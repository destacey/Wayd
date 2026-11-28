using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Products;
using Wayd.ProductManagement.Application.Releases.Dtos;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Releases.Queries;

/// <summary>
/// The releases and versions on the teams' Proposed or Active products that are still to ship and targeted on or
/// before the window's last day, past target included, earliest target first.
/// </summary>
/// <remarks>
/// The teams' products are those <c>GetTeamsProductCountQuery</c> counts — direct links, Proposed or Active — so the
/// home page, which shows its releases widget only while that count is above zero, never hides one this returns.
/// A version reaches them by being cut against one. A release is usually announced under a product line, or under
/// none, while teams are linked to the most specific product, so a release reaches a team product when it is
/// announced under that product or a line above it, or when it carries a version of it directly or as a changed
/// line of a package; a carried-forward line ships nothing new, so it does not count. The caller supplies the teams,
/// since Product Management holds no memberships.
/// </remarks>
/// <param name="TeamIds">The ids of the teams and teams of teams.</param>
/// <param name="Today">Today in the system time zone; a target date before it is past target.</param>
/// <param name="Days">How many days after today the window runs to, inclusive.</param>
/// <param name="IncludeReleases">Whether to read releases; false leaves <c>Releases</c> null.</param>
/// <param name="IncludeVersions">Whether to read versions; false leaves <c>Versions</c> null.</param>
public sealed record GetTeamsUpcomingReleasesQuery(
    IReadOnlyCollection<Guid> TeamIds,
    LocalDate Today,
    int Days,
    bool IncludeReleases,
    bool IncludeVersions) : IQuery<TeamProductReleasesDto>;

/// <summary>Keeps the window to a span a briefing can use.</summary>
public sealed class GetTeamsUpcomingReleasesQueryValidator : AbstractValidator<GetTeamsUpcomingReleasesQuery>
{
    /// <summary>The longest window, in days.</summary>
    public const int MaxDays = 90;

    public GetTeamsUpcomingReleasesQueryValidator()
    {
        RuleFor(q => q.Days)
            .InclusiveBetween(1, MaxDays)
            .WithMessage($"The window must be between 1 and {MaxDays} days.");
    }
}

/// <summary>Reads the teams' upcoming and past-target releases and versions.</summary>
public sealed class GetTeamsUpcomingReleasesQueryHandler(IProductManagementDbContext context)
    : IQueryHandler<GetTeamsUpcomingReleasesQuery, TeamProductReleasesDto>
{
    private readonly IProductManagementDbContext _context = context;

    public async Task<TeamProductReleasesDto> Handle(
        GetTeamsUpcomingReleasesQuery query,
        CancellationToken cancellationToken)
    {
        var nothing = new TeamProductReleasesDto
        {
            Releases = query.IncludeReleases ? [] : null,
            Versions = query.IncludeVersions ? [] : null,
        };

        if (query.TeamIds.Count == 0 || (!query.IncludeReleases && !query.IncludeVersions))
        {
            return nothing;
        }

        var teamIds = query.TeamIds;
        var teamProductIds = (await _context.Products
            .AsNoTracking()
            .Where(p => p.StatusCategory == StatusCategory.Proposed || p.StatusCategory == StatusCategory.Active)
            .Where(p => p.TeamLinks.Any(l => teamIds.Contains(l.TeamId)))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        if (teamProductIds.Count == 0)
        {
            return nothing;
        }

        var catalog = await ProductCatalog.Load(_context, cancellationToken);
        var lastDay = query.Today.PlusDays(query.Days);

        return new TeamProductReleasesDto
        {
            Releases = query.IncludeReleases
                ? await Releases(teamProductIds, catalog, query.Today, lastDay, cancellationToken)
                : null,
            Versions = query.IncludeVersions
                ? await Versions(teamProductIds, catalog, query.Today, lastDay, cancellationToken)
                : null,
        };
    }

    private async Task<IReadOnlyList<TeamProductVersionDto>> Versions(
        HashSet<Guid> teamProductIds,
        ProductCatalog catalog,
        LocalDate today,
        LocalDate lastDay,
        CancellationToken cancellationToken)
    {
        var productIds = teamProductIds.ToList();
        var versions = await _context.Versions
            .AsNoTracking()
            .Where(v => v.StatusCategory == StatusCategory.Proposed || v.StatusCategory == StatusCategory.Active)
            .Where(v => v.TargetDate != null && v.TargetDate <= lastDay)
            .Where(v => productIds.Contains(v.ProductId))
            .Select(v => new
            {
                v.Id,
                v.Key,
                v.Number,
                v.Name,
                TargetDate = v.TargetDate!.Value,
                v.ProductId,
                Status = new StatusFields(v.StatusId, v.StatusName, v.StatusCategory, v.StatusAliasValue),
            })
            .ToListAsync(cancellationToken);

        return [.. versions
            .Select(v => new TeamProductVersionDto
            {
                Id = v.Id,
                Key = v.Key,
                Number = v.Number,
                Name = v.Name,
                TargetDate = v.TargetDate,
                IsPastTarget = v.TargetDate < today,
                Status = v.Status.ToDto(ProductWorkflowOwners.Version.Key),
                Product = catalog.Navigation(v.ProductId),
            })
            .OrderBy(v => v.TargetDate)
            .ThenBy(v => v.Key)];
    }

    private async Task<IReadOnlyList<TeamProductReleaseDto>> Releases(
        HashSet<Guid> teamProductIds,
        ProductCatalog catalog,
        LocalDate today,
        LocalDate lastDay,
        CancellationToken cancellationToken)
    {
        // A release announced under a product reaches every team product at or beneath it.
        var teamProductsUnder = teamProductIds
            .SelectMany(t => catalog.Ancestry(t)
                .Select(line => line.Id)
                .Append(t)
                .Select(line => (Line: line, TeamProduct: t)))
            .ToLookup(x => x.Line, x => x.TeamProduct);
        var announcingIds = teamProductsUnder.Select(g => g.Key).ToList();
        var productIds = teamProductIds.ToList();
        var teamVersionIds = _context.Versions
            .Where(v => productIds.Contains(v.ProductId))
            .Select(v => v.Id);
        var packagesChangingTeamProducts = _context.ReleasePackageComponents
            .Where(c => c.Kind == ManifestEntryKind.Changed && productIds.Contains(c.ProductId))
            .Select(c => c.PackageId);

        var candidates = await _context.Releases
            .AsNoTracking()
            .Where(r => r.StatusCategory == StatusCategory.Proposed || r.StatusCategory == StatusCategory.Active)
            .Where(r => r.TargetDate != null && r.TargetDate <= lastDay)
            .Where(r => (r.ProductId != null && announcingIds.Contains(r.ProductId.Value))
                || _context.ReleaseVersions.Any(rv => rv.ReleaseId == r.Id && teamVersionIds.Contains(rv.VersionId))
                || _context.ReleasePackageInclusions.Any(i =>
                    i.ReleaseId == r.Id && packagesChangingTeamProducts.Contains(i.PackageId)))
            .Select(r => new
            {
                r.Id,
                r.Key,
                r.Version,
                r.Name,
                TargetDate = r.TargetDate!.Value,
                r.ProductId,
                Status = new StatusFields(r.StatusId, r.StatusName, r.StatusCategory, r.StatusAliasValue),
            })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return [];
        }

        var releaseIds = candidates.Select(r => r.Id).ToList();

        var carriedDirectly = await _context.ReleaseVersions
            .Where(rv => releaseIds.Contains(rv.ReleaseId))
            .Join(
                _context.Versions.Where(v => productIds.Contains(v.ProductId)),
                rv => rv.VersionId,
                v => v.Id,
                (rv, v) => new { rv.ReleaseId, v.ProductId })
            .ToListAsync(cancellationToken);

        var carriedInPackages = await _context.ReleasePackageInclusions
            .Where(i => releaseIds.Contains(i.ReleaseId))
            .Join(
                _context.ReleasePackageComponents
                    .Where(c => c.Kind == ManifestEntryKind.Changed && productIds.Contains(c.ProductId)),
                i => i.PackageId,
                c => c.PackageId,
                (i, c) => new { i.ReleaseId, c.ProductId })
            .ToListAsync(cancellationToken);

        var carried = carriedDirectly
            .Concat(carriedInPackages)
            .ToLookup(c => c.ReleaseId, c => c.ProductId);

        return [.. candidates
            .Select(release =>
            {
                var reached = new HashSet<Guid>(carried[release.Id]);
                if (release.ProductId is { } announcedUnder)
                {
                    reached.UnionWith(teamProductsUnder[announcedUnder]);
                }

                return new TeamProductReleaseDto
                {
                    Id = release.Id,
                    Key = release.Key,
                    Version = release.Version,
                    Name = release.Name,
                    TargetDate = release.TargetDate,
                    IsPastTarget = release.TargetDate < today,
                    Status = release.Status.ToDto(ProductWorkflowOwners.Release.Key),
                    Product = release.ProductId is { } productId ? catalog.Navigation(productId) : null,
                    TeamProducts = [.. reached
                        .Select(catalog.Navigation)
                        .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(p => p.Key)],
                };
            })
            .OrderBy(r => r.TargetDate)
            .ThenBy(r => r.Key)];
    }

    /// <summary>The status columns a row reads, so the query need not load the whole record.</summary>
    private sealed record StatusFields(Guid Id, string Name, StatusCategory Category, int AliasValue)
    {
        public StatusNavigationDto ToDto(string ownerType) =>
            new()
            {
                Id = Id,
                Name = Name,
                Category = Category,
                CategoryLabel = WorkflowOwners.CategoryLabel(ownerType, Category),
                Alias = AliasValue,
            };
    }
}
