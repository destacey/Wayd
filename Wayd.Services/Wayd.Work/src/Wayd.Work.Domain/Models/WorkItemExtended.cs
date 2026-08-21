namespace Wayd.Work.Domain.Models;

// TODO: convert this to an entity value attribute architecture
public sealed class WorkItemExtended
{
    private WorkItemExtended() { }

    private WorkItemExtended(
        Guid id,
        string? iterationPath,
        string? assignedToExternalId,
        string? createdByExternalId,
        string? lastModifiedByExternalId)
    {
        Id = id;
        ExternalTeamIdentifier = iterationPath;
        AssignedToExternalId = assignedToExternalId;
        CreatedByExternalId = createdByExternalId;
        LastModifiedByExternalId = lastModifiedByExternalId;
    }

    /// <summary>
    /// Work Item Id
    /// </summary>
    public Guid Id { get; init; }

    public string? ExternalTeamIdentifier { get; private set; }

    /// <summary>
    /// The external system's identity id for the person this item is assigned to, as reported by
    /// the sync. Only the id is kept — the address, display name, and handle live once on the
    /// matching <c>ExternalIdentityMapping</c> row rather than being copied onto every work item,
    /// where they would go stale the moment a person is renamed.
    /// </summary>
    /// <remarks>
    /// This is what lets an admin's mapping decision reach work already synced: without it, an
    /// item that failed to resolve records only a null employee, and nothing says which external
    /// identity it should have belonged to.
    /// </remarks>
    public string? AssignedToExternalId { get; private set; }

    /// <summary>The external system's identity id for this item's author.</summary>
    public string? CreatedByExternalId { get; private set; }

    /// <summary>The external system's identity id for whoever last changed this item.</summary>
    public string? LastModifiedByExternalId { get; private set; }

    public void Update(WorkItemExtended? workItemExtended)
    {
        ExternalTeamIdentifier = workItemExtended?.ExternalTeamIdentifier;
        AssignedToExternalId = workItemExtended?.AssignedToExternalId;
        CreatedByExternalId = workItemExtended?.CreatedByExternalId;
        LastModifiedByExternalId = workItemExtended?.LastModifiedByExternalId;
    }

    /// <summary>
    /// Creates the row only when the sync actually reported something to keep, so an item with no
    /// extended data does not carry an empty row.
    /// </summary>
    /// <remarks>
    /// Every field counts toward "is this row needed", not just the team identifier. Keying on the
    /// identifier alone would drop the external identity ids for any item without an iteration
    /// path — and <see cref="WorkItem.Update"/> clears <c>ExtendedProps</c> outright when handed
    /// null, so those ids would be silently lost on the next sync.
    /// </remarks>
    public static WorkItemExtended? Create(
        Guid id,
        string? externalTeamIdentifier,
        string? assignedToExternalId = null,
        string? createdByExternalId = null,
        string? lastModifiedByExternalId = null)
    {
        return string.IsNullOrWhiteSpace(externalTeamIdentifier)
            && string.IsNullOrWhiteSpace(assignedToExternalId)
            && string.IsNullOrWhiteSpace(createdByExternalId)
            && string.IsNullOrWhiteSpace(lastModifiedByExternalId)
            ? null
            : new WorkItemExtended(
                id,
                externalTeamIdentifier,
                assignedToExternalId,
                createdByExternalId,
                lastModifiedByExternalId);
    }
}
