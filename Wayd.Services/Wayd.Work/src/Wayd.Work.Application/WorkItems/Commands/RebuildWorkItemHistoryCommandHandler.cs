using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkItems.Commands;

/// <summary>
/// Rebuilds the <see cref="WorkItemStateHistory"/> of the workspace's work items from their stored
/// <see cref="WorkItemSourceRevision"/>s, as an out-of-order revision rebuilds one item's.
/// </summary>
/// <remarks>
/// Works through the items a batch at a time, each batch in its own transaction, so a large workspace never holds
/// one long transaction, and a rebuild that stops part way leaves every item with a complete history: rebuilt, or
/// as it was. An item's every revision is replayed, including those read while it was in another workspace, each
/// resolved against the workspace that read it, as the history sync resolves them.
/// </remarks>
public sealed class RebuildWorkItemHistoryCommandHandler(
    IWorkDbContext workDbContext,
    IStatusWorkflowDbContext statusWorkflowDbContext,
    IStatusResolver statusResolver,
    ILogger<RebuildWorkItemHistoryCommandHandler> logger)
    : ICommandHandler<RebuildWorkItemHistoryCommand, int>
{
    /// <summary>How many items are rebuilt per transaction; also keeps IN lists inside SQL Server's parameter limit.</summary>
    private const int BatchSize = 500;

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IStatusWorkflowDbContext _statusWorkflowDbContext = statusWorkflowDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ILogger<RebuildWorkItemHistoryCommandHandler> _logger = logger;

    public async Task<Result<int>> Handle(RebuildWorkItemHistoryCommand request, CancellationToken cancellationToken)
    {
        var workspace = await _workDbContext.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.WorkspaceId && w.OwnershipInfo.Ownership == Ownership.Managed, cancellationToken);
        if (workspace is null)
            return Result.Failure<int>($"Unable to rebuild the work item history of workspace {request.WorkspaceId} because the workspace does not exist.");

        // The workspace's statuses are loaded once; each batch loads only the iterations and people it names.
        var loaded = await WorkItemRevisionResolver.Load(_workDbContext, _statusWorkflowDbContext, _statusResolver, workspace, request.ConnectionId, cancellationToken);
        if (loaded.IsFailure)
            return Result.Failure<int>(loaded.Error);

        var written = 0;
        var items = 0;
        var offset = 0;

        while (true)
        {
            // Paged by offset over a stable order: the rebuild rewrites periods, never the items being paged.
            var workItemIds = await _workDbContext.WorkItems
                .Where(w => w.WorkspaceId == workspace.Id)
                .OrderBy(w => w.Id)
                .Skip(offset)
                .Take(BatchSize)
                .Select(w => w.Id)
                .ToListAsync(cancellationToken);

            if (workItemIds.Count == 0)
                break;

            var result = await RebuildBatch(workspace, loaded.Value, workItemIds, cancellationToken);
            if (result.IsFailure)
                return result;

            written += result.Value;
            items += workItemIds.Count;
            offset += workItemIds.Count;
        }

        _logger.LogInformation(
            "Rebuilt the work item history of workspace {WorkspaceId}: {ItemCount} items, {WrittenCount} periods written.",
            workspace.Id, items, written);

        return written;
    }

    private async Task<Result<int>> RebuildBatch(Workspace workspace, WorkItemRevisionResolver workspaceResolver, List<Guid> workItemIds, CancellationToken cancellationToken)
    {
        await using var unitOfWork = await _workDbContext.BeginUnitOfWork(cancellationToken);

        var revisions = await _workDbContext.WorkItemSourceRevisions
            .AsNoTracking()
            .Where(r => workItemIds.Contains(r.WorkItemId))
            .ToListAsync(cancellationToken);

        var resolver = await workspaceResolver.For(revisions, cancellationToken);

        // Set-based, ahead of the save that inserts the replacements, so the unique open-period and revision
        // indexes never see both at once. Items with no stored revision keep what they have.
        var rebuiltIds = revisions.Select(r => r.WorkItemId).Distinct().ToList();
        await _workDbContext.WorkItemStateHistory
            .Where(h => rebuiltIds.Contains(h.WorkItemId))
            .ExecuteDeleteAsync(cancellationToken);

        var periods = new List<WorkItemStateHistory>();
        foreach (var item in revisions.GroupBy(r => r.WorkItemId))
        {
            var timeline = new WorkItemStateTimeline(item.Key, workspace.Id, null, 0);
            foreach (var revision in item.OrderBy(r => r.Revision))
            {
                var period = timeline.Apply(revision.Revision, revision.Changed, resolver.Resolve(revision), revision.WorkspaceId);
                if (period is not null)
                    periods.Add(period);
            }
        }

        if (periods.Count > 0)
            await _workDbContext.WorkItemStateHistory.AddRangeAsync(periods, cancellationToken);

        await _workDbContext.SaveChangesAsync(cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        // Nothing reads the written periods again, and a tracker holding every batch's would make each later save
        // slower than the last. The command runs in a scope of its own, so clearing loses nothing.
        _workDbContext.ChangeTracker?.Clear();

        return periods.Count;
    }
}
