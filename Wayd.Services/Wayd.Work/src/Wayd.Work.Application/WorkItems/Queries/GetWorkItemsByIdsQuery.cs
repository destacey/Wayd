using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkItems.Queries;

/// <summary>
/// The work items with the given ids, for a caller that holds work item ids from another module and shows each
/// by its current key, title and status.
/// </summary>
/// <remarks>
/// An id with no work item is left out rather than failing the call, since a work item deleted in its source
/// system is deleted here too and the caller decides how to show it.
/// </remarks>
/// <param name="Ids">The work items, by id.</param>
public sealed record GetWorkItemsByIdsQuery(IReadOnlyCollection<Guid> Ids) : IQuery<IReadOnlyList<WorkItemDetailsNavigationDto>>;

/// <summary>Answers <see cref="GetWorkItemsByIdsQuery"/>.</summary>
public sealed class GetWorkItemsByIdsQueryHandler(IWorkDbContext workDbContext)
    : IQueryHandler<GetWorkItemsByIdsQuery, IReadOnlyList<WorkItemDetailsNavigationDto>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    /// <summary>The work items found, ordered by id; an unknown id is absent.</summary>
    public async Task<IReadOnlyList<WorkItemDetailsNavigationDto>> Handle(GetWorkItemsByIdsQuery request, CancellationToken cancellationToken)
    {
        if (request.Ids.Count == 0)
            return [];

        var ids = request.Ids.Distinct().ToList();

        return await _workDbContext.WorkItems
            .Where(w => ids.Contains(w.Id))
            .OrderBy(w => w.Id)
            .ProjectToType<WorkItemDetailsNavigationDto>()
            .ToListAsync(cancellationToken);
    }
}
