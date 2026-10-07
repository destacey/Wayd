using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkItems.Queries;

/// <summary>
/// Finds the workspace's work items with a gap in their stored revisions, so the sync can fetch the
/// missing ones from the item itself.
/// </summary>
/// <remarks>
/// Revisions are numbered from 1 without gaps, so an item is complete when it holds as many
/// revisions as its highest number; one with no stored revision is missing them all. An item already
/// filled at its current highest revision is passed over: its gap is one the source would not close,
/// and asking again on every sync would only spend calls and hold back the items after it.
/// </remarks>
public sealed class GetWorkItemsMissingRevisionsQueryHandler(IWorkDbContext workDbContext) : IQueryHandler<GetWorkItemsMissingRevisionsQuery, Result<IReadOnlyList<int>>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    public async Task<Result<IReadOnlyList<int>>> Handle(GetWorkItemsMissingRevisionsQuery request, CancellationToken cancellationToken)
    {
        var workItems = _workDbContext.WorkItems
            .Where(w => w.WorkspaceId == request.WorkspaceId && w.ExternalId != null);

        var revisions = _workDbContext.WorkItemSourceRevisions;
        var fills = _workDbContext.WorkItemRevisionFills;

        var withoutRevisions =
            from w in workItems
            where !revisions.Any(r => r.WorkItemId == w.Id) && !fills.Any(f => f.WorkItemId == w.Id)
            select w.ExternalId!.Value;

        // One grouped pass over the workspace's revisions, rather than a count and a max per item.
        var workItemIds = workItems.Select(w => w.Id);
        var withGaps =
            from s in revisions
                .Where(r => workItemIds.Contains(r.WorkItemId))
                .GroupBy(r => r.WorkItemId)
                .Select(g => new { WorkItemId = g.Key, Count = g.Count(), Highest = g.Max(r => r.Revision) })
            where s.Count != s.Highest && !fills.Any(f => f.WorkItemId == s.WorkItemId && f.HighestRevision >= s.Highest)
            join w in workItems on s.WorkItemId equals w.Id
            select w.ExternalId!.Value;

        var ids = await withoutRevisions
            .Concat(withGaps)
            .OrderBy(id => id)
            .Take(request.Limit)
            .ToListAsync(cancellationToken);

        return ids;
    }
}
