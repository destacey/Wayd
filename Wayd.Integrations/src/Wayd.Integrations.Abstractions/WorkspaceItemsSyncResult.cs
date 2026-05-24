namespace Wayd.Integrations.Abstractions;

/// <summary>
/// Counters returned from <see cref="IWorkItemSource.SyncWorkItems"/>. Sub-step failures inside
/// the source are surfaced via <see cref="HadPartialFailure"/> and <see cref="PartialFailureMessage"/>
/// rather than as a top-level <see cref="Result"/> failure — this preserves the "attempt each step
/// independently" semantics of the AzDO sync.
/// </summary>
public sealed record WorkspaceItemsSyncResult(
    int WorkItemsProcessed,
    int ParentLinkChangesProcessed,
    int DependencyLinkChangesProcessed,
    int DeletedWorkItemsProcessed,
    bool HadPartialFailure = false,
    string? PartialFailureMessage = null)
{
    public static readonly WorkspaceItemsSyncResult Zero = new(0, 0, 0, 0);

    public int TotalProcessed =>
        WorkItemsProcessed + ParentLinkChangesProcessed + DependencyLinkChangesProcessed + DeletedWorkItemsProcessed;
}
