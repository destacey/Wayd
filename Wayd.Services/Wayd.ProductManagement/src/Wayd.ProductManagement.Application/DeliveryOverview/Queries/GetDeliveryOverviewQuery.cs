using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.DeliveryOverview.Dtos;

namespace Wayd.ProductManagement.Application.DeliveryOverview.Queries;

/// <summary>
/// Version activity over a window, for one product subtree or the whole catalog.
/// </summary>
/// <param name="ProductId">
/// Narrows to this node <em>and everything beneath it</em>. Selecting a grouping that has no versions
/// of its own therefore rolls up its children rather than reporting nothing.
/// </param>
public sealed record GetDeliveryOverviewQuery(LocalDate From, LocalDate To, Guid? ProductId = null)
    : IQuery<DeliveryOverviewDto>;

public sealed class GetDeliveryOverviewQueryValidator : AbstractValidator<GetDeliveryOverviewQuery>
{
    public GetDeliveryOverviewQueryValidator()
    {
        RuleFor(q => q.To)
            .GreaterThanOrEqualTo(q => q.From)
            .WithMessage("The end of the window cannot be before its start.");
    }
}

public sealed class GetDeliveryOverviewQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetDeliveryOverviewQuery, DeliveryOverviewDto>
{
    private const int Withdrawn = (int)ProductStatusAlias.Withdrawn;

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    /// <summary>One catalog node, reduced to what scoping and grouping need.</summary>
    private sealed record CatalogNode(Guid Id, int Key, string Name, Guid? ParentId, bool IsReleasable);

    /// <summary>One released version, reduced to what every figure here needs.</summary>
    private sealed record ReleasedVersion(
        Guid ProductId,
        LocalDate ReleasedDate,
        LocalDate? CutDate,
        int StatusAliasValue);

    public async Task<DeliveryOverviewDto> Handle(
        GetDeliveryOverviewQuery query, CancellationToken cancellationToken)
    {
        // The catalog is curated reference data — tens of rows, not thousands — so the subtree is
        // resolved by walking it here. A recursive CTE would be the alternative, and LINQ does not
        // express one.
        var catalog = await _productManagementDbContext.Products
            .Select(p => new CatalogNode(
                p.Id,
                p.Key,
                p.Name,
                p.ParentId,
                _productManagementDbContext.ProductTypes.Any(t => t.Id == p.ProductTypeId && t.IsReleasable)))
            .ToListAsync(cancellationToken);

        var childrenByParent = catalog
            .Where(p => p.ParentId is not null)
            .GroupBy(p => p.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Id).ToList());

        var inScope = query.ProductId is null
            ? catalog.Select(p => p.Id).ToHashSet()
            : Descendants(query.ProductId.Value, childrenByParent);

        var windowDays = Period.Between(query.From, query.To, PeriodUnits.Days).Days + 1;
        var previousTo = query.From.PlusDays(-1);
        var previousFrom = previousTo.PlusDays(-(windowDays - 1));

        var released = await ReleasedBetween(previousFrom, query.To, inScope, cancellationToken);

        var current = released.Where(v => v.ReleasedDate >= query.From).ToList();
        var previous = released.Where(v => v.ReleasedDate < query.From).ToList();

        var byProduct = catalog.ToDictionary(p => p.Id);

        return new DeliveryOverviewDto
        {
            Scope = new DeliveryScopeDto
            {
                Product = query.ProductId is not null && byProduct.TryGetValue(query.ProductId.Value, out var scoped)
                    ? NavigationDto.Create(scoped.Id, scoped.Key, scoped.Name)
                    : null,
                ReleasableNodeCount = catalog.Count(p => inScope.Contains(p.Id) && p.IsReleasable),
            },
            Frequency = new ReleaseFrequencyDto
            {
                Count = current.Count,
                WindowDays = windowDays,
                PerWeek = PerWeek(current.Count, windowDays),
                // Null rather than zero: a window with no releases gives no baseline to compare
                // against, and rendering it as a rise from zero would invent a trend.
                PreviousPerWeek = previous.Count == 0 ? null : PerWeek(previous.Count, windowDays),
            },
            CutToReleased = Latency(current, previous),
            Activity = BuildActivity(current, catalog, inScope, query.ProductId, childrenByParent),
        };
    }

    /// <summary>A node and everything beneath it.</summary>
    /// <remarks>
    /// Tracks what it has seen so data that already contains a cycle cannot loop forever. The domain
    /// refuses to create one, so this guards against a catalog that is already wrong.
    /// </remarks>
    private static HashSet<Guid> Descendants(Guid root, Dictionary<Guid, List<Guid>> childrenByParent)
    {
        var scope = new HashSet<Guid> { root };
        var pending = new Queue<Guid>([root]);

        while (pending.TryDequeue(out var id))
        {
            if (!childrenByParent.TryGetValue(id, out var children))
            {
                continue;
            }

            foreach (var child in children.Where(scope.Add))
            {
                pending.Enqueue(child);
            }
        }

        return scope;
    }

    private async Task<List<ReleasedVersion>> ReleasedBetween(
        LocalDate from, LocalDate to, HashSet<Guid> inScope, CancellationToken cancellationToken)
    {
        var versions = await _productManagementDbContext.Versions
            .Where(v => v.ReleasedDate != null
                && v.ReleasedDate >= from
                && v.ReleasedDate <= to)
            .Select(v => new ReleasedVersion(v.ProductId, v.ReleasedDate!.Value, v.CutDate, v.StatusAliasValue))
            .ToListAsync(cancellationToken);

        // Filtered here rather than with a Contains over the scope: the id set can be the whole
        // catalog, and a parameter list that size is worse than a pass over a window's releases.
        return versions.Where(v => inScope.Contains(v.ProductId)).ToList();
    }

    private static double PerWeek(int count, double windowDays) =>
        windowDays <= 0 ? 0 : count / windowDays * 7;

    private static CutToReleasedDto Latency(
        IReadOnlyCollection<ReleasedVersion> current, IReadOnlyCollection<ReleasedVersion> previous)
    {
        var measured = Measurable(current);

        return new CutToReleasedDto
        {
            AverageDays = measured.Count == 0 ? null : measured.Average(),
            MeasuredCount = measured.Count,
            ReleasedCount = current.Count,
            PreviousAverageDays = Measurable(previous) is { Count: > 0 } before ? before.Average() : null,
        };
    }

    /// <summary>
    /// Days from cut to release, for the versions that were actually cut.
    /// </summary>
    /// <remarks>
    /// A version released without a cut date carries no latency. Excluded rather than counted as
    /// zero, which would pull the mean down every time someone backfills history.
    /// </remarks>
    private static List<double> Measurable(IEnumerable<ReleasedVersion> versions) =>
        versions
            .Where(v => v.CutDate is not null)
            .Select(v => (double)Period.Between(v.CutDate!.Value, v.ReleasedDate, PeriodUnits.Days).Days)
            .ToList();

    /// <summary>
    /// The scope's subtree, depth-first, one row per node.
    /// </summary>
    /// <remarks>
    /// Flattened here rather than nested so the view indents instead of grouping under a parent's
    /// name. Grouping duplicated any node that is both releasable and a parent — it appeared once as
    /// a row and again as a heading over its own children.
    /// <para>
    /// A grouping is kept only when something releasable sits beneath it. One that leads nowhere is
    /// an empty heading, and a product with no releases is still a row, since an absent row reads as
    /// "not in scope" rather than "nothing shipped".
    /// </para>
    /// </remarks>
    private static List<ProductActivityDto> BuildActivity(
        IReadOnlyCollection<ReleasedVersion> released,
        IReadOnlyCollection<CatalogNode> catalog,
        HashSet<Guid> inScope,
        Guid? scopeRoot,
        Dictionary<Guid, List<Guid>> childrenByParent)
    {
        var versionsByProduct = released
            .GroupBy(v => v.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var byId = catalog.ToDictionary(node => node.Id);

        var roots = scopeRoot is not null
            ? new List<Guid> { scopeRoot.Value }
            : [.. catalog.Where(node => node.ParentId is null).Select(node => node.Id)];

        var rows = new List<ProductActivityDto>();

        void Walk(Guid id, int depth)
        {
            if (!byId.TryGetValue(id, out var node) || !inScope.Contains(id))
            {
                return;
            }

            var children = childrenByParent.TryGetValue(id, out var found)
                ? found
                    .Where(inScope.Contains)
                    .Where(byId.ContainsKey)
                    .OrderBy(child => byId[child].Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : [];

            // A grouping that leads to nothing releasable is a heading over an empty list.
            if (!node.IsReleasable && !LeadsToReleasable(id))
            {
                return;
            }

            var versions = versionsByProduct.TryGetValue(id, out var v) ? v : [];

            rows.Add(new ProductActivityDto
            {
                Product = NavigationDto.Create(node.Id, node.Key, node.Name),
                Depth = depth,
                IsReleasable = node.IsReleasable,
                TotalReleased = versions.Count,
                Days =
                [
                    .. versions
                        .GroupBy(version => version.ReleasedDate)
                        .OrderBy(day => day.Key)
                        .Select(day => new DailyReleaseCountDto
                        {
                            Date = day.Key,
                            Released = day.Count(),
                            Withdrawn = day.Count(version => version.StatusAliasValue == Withdrawn),
                        }),
                ],
            });

            foreach (var child in children)
            {
                Walk(child, depth + 1);
            }
        }

        bool LeadsToReleasable(Guid id)
        {
            if (!childrenByParent.TryGetValue(id, out var children))
            {
                return false;
            }

            return children
                .Where(inScope.Contains)
                .Any(child =>
                    (byId.TryGetValue(child, out var node) && node.IsReleasable)
                    || LeadsToReleasable(child));
        }

        foreach (var root in roots.OrderBy(id => byId.TryGetValue(id, out var node) ? node.Name : string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            Walk(root, 0);
        }

        return rows;
    }
}
