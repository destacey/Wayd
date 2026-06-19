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
    bool HadPartialFailure,
    string? Error)
{
    public static WorkspaceSyncDetail FromSuccess(WorkspaceSyncTarget target, WorkspaceItemsSyncResult r) =>
        new(target.InternalWorkspaceId, target.WorkspaceName, true,
            r.WorkItemsProcessed, r.ParentLinkChangesProcessed, r.DependencyLinkChangesProcessed, r.DeletedWorkItemsProcessed,
            r.HadPartialFailure, r.PartialFailureMessage);

    public static WorkspaceSyncDetail FromFailure(WorkspaceSyncTarget target, string error) =>
        new(target.InternalWorkspaceId, target.WorkspaceName, false, 0, 0, 0, 0, false, error);
}
