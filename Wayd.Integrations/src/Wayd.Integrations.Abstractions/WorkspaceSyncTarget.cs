namespace Wayd.Integrations.Abstractions;

/// <summary>
/// A single workspace the runner should sync. The source computes the list of targets in
/// <see cref="IWorkItemSource.GetSyncPlan"/> — for connectors with intermediate hierarchy
/// (e.g. AzDO's WorkProcess → Workspace), the hierarchy is walked internally and the result
/// is a flat list. Filters carry any connector-specific per-workspace context.
/// </summary>
public sealed record WorkspaceSyncTarget(
    Guid ExternalWorkspaceId,
    Guid InternalWorkspaceId,
    string WorkspaceName,
    string WorkspaceKey,
    WorkItemSyncFilters Filters);
