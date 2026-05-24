namespace Wayd.Integrations.Abstractions;

/// <summary>
/// Connector-neutral contract for pulling work items from an external system. One implementation
/// per connector, registered as a keyed transient by <see cref="Connector"/> value. The runner
/// (<c>IWorkSyncRunner</c>) consumes this interface and is unaware of any connector-specific concepts
/// such as Azure DevOps work processes or Jira projects.
/// </summary>
public interface IWorkItemSource
{
    /// <summary>The connector this source serves.</summary>
    Connector Connector { get; }

    /// <summary>
    /// Bind this source instance to a specific connection. Called once by the factory before any
    /// sync method is invoked. Sources should cast the descriptor's boxed configuration here and
    /// fail fast if the shape is wrong.
    /// </summary>
    Result Bind(SyncableConnectionDescriptor descriptor);

    /// <summary>Cheap reachability/auth check.</summary>
    Task<Result> TestConnection(CancellationToken cancellationToken);

    /// <summary>Returns the external system identifier (e.g. AzDO instance id).</summary>
    Task<Result<string>> GetSystemId(CancellationToken cancellationToken);

    /// <summary>
    /// Refresh the connection's inventory of processes / workspaces / teams from the external
    /// system. Called once per connection at the start of every sync run.
    /// </summary>
    Task<Result> RefreshOrganizationConfiguration(Guid syncId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the flat list of workspaces to sync for this connection. For connectors with
    /// intermediate hierarchy the source walks it internally; the runner only sees workspaces.
    /// </summary>
    Task<Result<IReadOnlyList<WorkspaceSyncTarget>>> GetSyncPlan(CancellationToken cancellationToken);

    /// <summary>
    /// Prepare a workspace for item sync — refresh whatever schema/metadata the connector requires
    /// before <see cref="SyncIterations"/> and <see cref="SyncWorkItems"/> will produce correct
    /// results. For AzDO this is work-process (types/statuses/workflow) + workspace metadata.
    /// Failure is blocking for the workspace (the runner skips iterations/items).
    /// </summary>
    Task<Result> PrepareWorkspaceForItemSync(WorkspaceSyncTarget target, Guid syncId, CancellationToken cancellationToken);

    /// <summary>Sync iteration / sprint structure for a workspace.</summary>
    Task<Result> SyncIterations(WorkspaceSyncTarget target, string systemId, Guid syncId, CancellationToken cancellationToken);

    /// <summary>
    /// Sync work items and their relationship changes for a workspace. The source decides the
    /// "since" cutoff internally based on <paramref name="syncType"/> and returns aggregate counters.
    /// </summary>
    Task<Result<WorkspaceItemsSyncResult>> SyncWorkItems(
        WorkspaceSyncTarget target,
        SyncType syncType,
        Guid syncId,
        CancellationToken cancellationToken);
}
