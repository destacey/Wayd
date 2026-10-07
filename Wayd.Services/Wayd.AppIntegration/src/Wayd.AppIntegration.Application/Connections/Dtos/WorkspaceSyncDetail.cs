using Wayd.Integrations.Abstractions;

namespace Wayd.AppIntegration.Application.Connections.Dtos;

/// <summary>
/// Per-workspace breakdown of a single sync run, serialized into <c>SyncRun.DetailsJson</c>
/// by <see cref="Managers.WorkSyncRunner"/> and read back by the sync-run details query.
/// </summary>
public sealed record WorkspaceSyncDetail(
    Guid InternalWorkspaceId,
    string WorkspaceName,
    bool Succeeded,
    int WorkItemsProcessed,
    int ParentLinkChangesProcessed,
    int DependencyLinkChangesProcessed,
    int DeletedWorkItemsProcessed,
    int WorkItemRevisionsProcessed,
    int WorkItemHistoryPeriodsWritten,
    bool HadPartialFailure,
    string? Error)
{
    public static WorkspaceSyncDetail FromSuccess(WorkspaceSyncTarget target, WorkspaceItemsSyncResult r, int workItemRevisionsProcessed, int workItemHistoryPeriodsWritten) =>
        new(target.InternalWorkspaceId, target.WorkspaceName, true,
            r.WorkItemsProcessed, r.ParentLinkChangesProcessed, r.DependencyLinkChangesProcessed, r.DeletedWorkItemsProcessed,
            workItemRevisionsProcessed, workItemHistoryPeriodsWritten, r.HadPartialFailure, r.PartialFailureMessage);

    /// <summary>
    /// Adds what filling the workspace's missing revisions did, which runs after every workspace's
    /// own sync. A fill error makes the workspace degraded, not failed: its items are synced.
    /// </summary>
    public WorkspaceSyncDetail WithRevisionFill(int revisionsProcessed, int periodsWritten, string? error)
    {
        var message = error is null ? null : $"Filling missing work item revisions: {error}";
        return this with
        {
            WorkItemRevisionsProcessed = WorkItemRevisionsProcessed + revisionsProcessed,
            WorkItemHistoryPeriodsWritten = WorkItemHistoryPeriodsWritten + periodsWritten,
            HadPartialFailure = HadPartialFailure || error is not null,
            Error = message is null ? Error : Error is null ? message : $"{Error}; {message}",
        };
    }

    public static WorkspaceSyncDetail FromFailure(WorkspaceSyncTarget target, string error) =>
        new(target.InternalWorkspaceId, target.WorkspaceName, false, 0, 0, 0, 0, 0, 0, false, error);
}
