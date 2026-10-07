using NodaTime;
using Wayd.Common.Domain.Enums.Work;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// One period of a work item's history during which none of its tracked fields changed. A work
/// item's periods are contiguous and never overlap, so its state at an instant T is the one row
/// where <c>ValidFrom &lt;= T</c> and <c>ValidTo</c> is null or after T.
/// </summary>
/// <remarks>
/// Built from the item's <see cref="WorkItemSourceRevision"/>s, not raised by Wayd: writing a period
/// raises no domain event. A revision that arrives out of order rebuilds the item's periods. The rows
/// are deleted with their work item.
/// </remarks>
public sealed class WorkItemStateHistory : BaseEntity<long>
{
    private WorkItemStateHistory() { }

    private WorkItemStateHistory(Guid workItemId, Guid workspaceId, int revision, Instant validFrom, WorkItemTrackedState state)
    {
        WorkItemId = workItemId;
        WorkspaceId = workspaceId;
        Revision = revision;
        ValidFrom = validFrom;

        IterationId = state.IterationId;
        ExternalIterationId = state.ExternalIterationId;
        StatusId = state.StatusId;
        StatusName = state.StatusName;
        StatusCategory = state.StatusCategory;
        WorkTypeId = state.WorkTypeId;
        WorkTypeName = state.WorkTypeName;
        TeamKey = state.TeamKey;
        AssignedToId = state.AssignedToId;
        AssignedToExternalId = state.AssignedToExternalId;
        StoryPoints = state.StoryPoints;
        Effort = state.Effort;
        Size = state.Size;
    }

    /// <summary>The work item this period belongs to.</summary>
    public Guid WorkItemId { get; private init; }

    /// <summary>
    /// The workspace whose sync read the revision that opened the period. An item that moved between
    /// synced workspaces keeps the periods from before the move under the workspace it was in; time in
    /// a project no workspace syncs is recorded under the workspace the item is in.
    /// </summary>
    public Guid WorkspaceId { get; private init; }

    /// <summary>The source revision that opened the period.</summary>
    public int Revision { get; private init; }

    /// <summary>When the period began.</summary>
    public Instant ValidFrom { get; private init; }

    /// <summary>When the period ended; null while it is the item's current state.</summary>
    public Instant? ValidTo { get; private set; }

    /// <summary>The Wayd iteration; null when the item was in no iteration or one outside the synced set.</summary>
    public Guid? IterationId { get; private init; }

    /// <summary>The source's stable id for the iteration.</summary>
    public int? ExternalIterationId { get; private init; }

    /// <summary>The Wayd status; null when the source's status has no match.</summary>
    public int? StatusId { get; private init; }

    /// <summary>The status name as the source recorded it.</summary>
    public string StatusName { get; private init; } = null!;

    /// <summary>The status's category when the period was written; null when the status had no match.</summary>
    public WorkStatusCategory? StatusCategory { get; private init; }

    /// <summary>The Wayd work type; null when the source's type has no match in the workspace's process.</summary>
    public int? WorkTypeId { get; private init; }

    /// <summary>The work type name as the source recorded it.</summary>
    public string WorkTypeName { get; private init; } = null!;

    /// <summary>The source's own value for a team recorded on the item; null for a source whose team comes from the iteration.</summary>
    public string? TeamKey { get; private init; }

    /// <summary>
    /// The employee the item was assigned to; null when unassigned or the identity is unmapped. An
    /// admin's later identity mapping repoints it through <see cref="AssignedToExternalId"/>.
    /// </summary>
    public Guid? AssignedToId { get; private set; }

    /// <summary>The source's identity id for the assignee.</summary>
    public string? AssignedToExternalId { get; private init; }

    /// <summary>The story points estimate.</summary>
    public double? StoryPoints { get; private init; }

    /// <summary>The effort estimate.</summary>
    public double? Effort { get; private init; }

    /// <summary>The size estimate.</summary>
    public double? Size { get; private init; }

    /// <summary>The tracked values this period holds.</summary>
    public WorkItemTrackedState State => new(
        IterationId, ExternalIterationId, StatusId, StatusName, StatusCategory, WorkTypeId, WorkTypeName,
        TeamKey, AssignedToId, AssignedToExternalId, StoryPoints, Effort, Size);

    /// <summary>Opens a period, current until a later revision closes it.</summary>
    internal static WorkItemStateHistory Open(Guid workItemId, Guid workspaceId, int revision, Instant validFrom, WorkItemTrackedState state) =>
        new(workItemId, workspaceId, revision, validFrom, state);

    /// <summary>Ends the period at <paramref name="validTo"/>.</summary>
    internal void Close(Instant validTo)
    {
        ValidTo = validTo;
    }
}
