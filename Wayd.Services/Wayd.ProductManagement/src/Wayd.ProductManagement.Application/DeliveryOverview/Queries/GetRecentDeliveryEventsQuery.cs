using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.DeliveryOverview.Dtos;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.DeliveryOverview.Queries;

/// <summary>
/// What has happened to versions and packages lately, most recent first.
/// </summary>
/// <param name="ProductId">
/// Narrows to this node and everything beneath it. Packages are left out when it is set: a package
/// spans several products, so attributing one to a single node would put it under each of them.
/// </param>
public sealed record GetRecentDeliveryEventsQuery(int Take = 10, Guid? ProductId = null)
    : IQuery<IReadOnlyCollection<RecentDeliveryEventDto>>;

public sealed class GetRecentDeliveryEventsQueryValidator
    : AbstractValidator<GetRecentDeliveryEventsQuery>
{
    public GetRecentDeliveryEventsQueryValidator()
    {
        RuleFor(q => q.Take).InclusiveBetween(1, 50);
    }
}

/// <remarks>
/// Takes both contexts because the status history is deliberately not a navigation on the record —
/// it is reached as a set, keyed by owner type and record id together, since a record id alone is
/// only unique within an owner type. The two are views over one context, so this is one connection.
/// </remarks>
public sealed class GetRecentDeliveryEventsQueryHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusWorkflowDbContext statusWorkflowDbContext)
    : IQueryHandler<GetRecentDeliveryEventsQuery, IReadOnlyCollection<RecentDeliveryEventDto>>
{
    private static readonly string VersionOwner = ProductWorkflowOwners.Version.Key;
    private static readonly string PackageOwner = ProductWorkflowOwners.ReleasePackage.Key;

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusWorkflowDbContext _statusWorkflowDbContext = statusWorkflowDbContext;

    public async Task<IReadOnlyCollection<RecentDeliveryEventDto>> Handle(
        GetRecentDeliveryEventsQuery query, CancellationToken cancellationToken)
    {
        var inScope = await ResolveScope(query.ProductId, cancellationToken);

        var versionEvents = await VersionEvents(query.Take, inScope, cancellationToken);

        // A package belongs to no single product, so scoping to one excludes them rather than
        // guessing. Unscoped, they interleave with the version events by time.
        var packageEvents = query.ProductId is null
            ? await PackageEvents(query.Take, cancellationToken)
            : [];

        // One entry per record, not per transition. A record that was planned and then released the
        // same afternoon would otherwise take two adjacent rows saying nearly the same thing, and a
        // feed of eight would show four records. The record's own page carries the full history.
        return
        [
            .. versionEvents
                .Concat(packageEvents)
                .GroupBy(e => (e.Kind, e.RecordId))
                .Select(record => record
                    .OrderByDescending(e => e.ChangedOn)
                    .First())
                .OrderByDescending(e => e.ChangedOn)
                .ThenByDescending(e => e.RecordKey)
                .Take(query.Take),
        ];
    }

    /// <summary>The products in scope, or null for the whole catalog.</summary>
    private async Task<HashSet<Guid>?> ResolveScope(Guid? productId, CancellationToken cancellationToken)
    {
        if (productId is null)
        {
            return null;
        }

        var catalog = await _productManagementDbContext.Products
            .Select(p => new { p.Id, p.ParentId })
            .ToListAsync(cancellationToken);

        var childrenByParent = catalog
            .Where(p => p.ParentId is not null)
            .GroupBy(p => p.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Id).ToList());

        var scope = new HashSet<Guid> { productId.Value };
        var pending = new Queue<Guid>([productId.Value]);

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

    /// <remarks>
    /// Scoped in the database, not after the fact. Taking a window globally and filtering it in
    /// memory silently loses the scoped product's events whenever other products have been busier:
    /// they fall outside the rows SQL returned and there is nothing left to filter. The id set is a
    /// subtree — tens of nodes — so an IN clause over it costs nothing.
    /// </remarks>
    private async Task<List<RecentDeliveryEventDto>> VersionEvents(
        int take, HashSet<Guid>? inScope, CancellationToken cancellationToken)
    {
        var events =
            from transition in _statusWorkflowDbContext.StatusTransitions
            join version in _productManagementDbContext.Versions
                on transition.RecordId equals version.Id
            join product in _productManagementDbContext.Products
                on version.ProductId equals product.Id
            where transition.OwnerType == VersionOwner
            select new { transition, version, product };

        if (inScope is not null)
        {
            events = events.Where(e => inScope.Contains(e.version.ProductId));
        }

        return await events
            .OrderByDescending(e => e.transition.ChangedOn)
            // Read deeper than the page: several transitions can belong to one record, and they
            // collapse to a single entry below.
            .Take(take * 4)
            .Select(e => new RecentDeliveryEventDto
            {
                RecordId = e.version.Id,
                RecordKey = e.version.Key,
                Kind = DeliveryRecordKind.Version,
                Product = NavigationDto.Create(e.product.Id, e.product.Key, e.product.Name),
                Label = e.version.Number,
                StatusName = e.transition.ToStatusName,
                Alias = (ProductStatusAlias)e.transition.ToAlias,
                ChangedOn = e.transition.ChangedOn,
                ReleasedDate = e.version.ReleasedDate,
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<List<RecentDeliveryEventDto>> PackageEvents(
        int take, CancellationToken cancellationToken) =>
        await (
            from transition in _statusWorkflowDbContext.StatusTransitions
            join package in _productManagementDbContext.ReleasePackages
                on transition.RecordId equals package.Id
            where transition.OwnerType == PackageOwner
            orderby transition.ChangedOn descending
            select new RecentDeliveryEventDto
            {
                RecordId = package.Id,
                RecordKey = package.Key,
                Kind = DeliveryRecordKind.ReleasePackage,
                Product = null,
                Label = package.Version,
                StatusName = transition.ToStatusName,
                Alias = (ProductStatusAlias)transition.ToAlias,
                ChangedOn = transition.ChangedOn,
                ReleasedDate = package.ReleasedDate,
                ComponentCount = _productManagementDbContext.ReleasePackageComponents
                    .Count(c => c.PackageId == package.Id),
            })
            .Take(take * 4)
            .ToListAsync(cancellationToken);
}
