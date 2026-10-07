namespace Wayd.Work.Domain.Models;

/// <summary>
/// Records that a work item's missing revisions were last fetched from the item itself, and the
/// highest revision it held afterwards. An item whose gap the source cannot close (it denies access,
/// or reports a revision Wayd cannot store) is not fetched again until a newer revision arrives.
/// </summary>
/// <remarks>
/// Sync bookkeeping, not part of the item's history: it raises no domain events and is deleted with
/// its work item.
/// </remarks>
public sealed class WorkItemRevisionFill
{
    private WorkItemRevisionFill() { }

    private WorkItemRevisionFill(Guid workItemId, int highestRevision)
    {
        WorkItemId = workItemId;
        HighestRevision = highestRevision;
    }

    /// <summary>The work item.</summary>
    public Guid WorkItemId { get; private init; }

    /// <summary>The highest revision stored for the item when it was last filled; 0 when it had none.</summary>
    public int HighestRevision { get; private set; }

    /// <summary>Records a first fill of the item.</summary>
    public static WorkItemRevisionFill Create(Guid workItemId, int highestRevision) =>
        new(workItemId, highestRevision);

    /// <summary>Records a later fill of the item.</summary>
    public void Refilled(int highestRevision)
    {
        HighestRevision = highestRevision;
    }
}
