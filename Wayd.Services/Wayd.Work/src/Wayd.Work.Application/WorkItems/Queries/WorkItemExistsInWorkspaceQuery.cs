using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkItems.Queries;

/// <summary>Asks whether a work item exists and belongs to the given workspace.</summary>
/// <param name="WorkspaceId">The workspace's id.</param>
/// <param name="WorkItemId">The work item's id.</param>
public sealed record WorkItemExistsInWorkspaceQuery(Guid WorkspaceId, Guid WorkItemId) : IQuery<bool>;

/// <summary>Answers <see cref="WorkItemExistsInWorkspaceQuery"/>.</summary>
public sealed class WorkItemExistsInWorkspaceQueryHandler(IWorkDbContext workDbContext) : IQueryHandler<WorkItemExistsInWorkspaceQuery, bool>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    /// <summary>True when the work item exists in the workspace; false when it does not exist or is in another workspace.</summary>
    public async Task<bool> Handle(WorkItemExistsInWorkspaceQuery request, CancellationToken cancellationToken)
    {
        return await _workDbContext.WorkItems
            .AnyAsync(w => w.Id == request.WorkItemId && w.WorkspaceId == request.WorkspaceId, cancellationToken);
    }
}
