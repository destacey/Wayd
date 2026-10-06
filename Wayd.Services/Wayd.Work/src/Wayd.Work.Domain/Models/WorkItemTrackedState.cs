using Wayd.Common.Domain.Enums.Work;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// The values of a work item's tracked fields at one revision: the fields whose history sprint
/// metrics read. Each value the source recorded is kept beside what Wayd resolved it to, because a
/// revision can reference a status, type, iteration or person Wayd no longer holds or never synced.
/// </summary>
/// <param name="IterationId">The Wayd iteration; null when the item was in no iteration or one outside the synced set.</param>
/// <param name="ExternalIterationId">The source's stable id for the iteration.</param>
/// <param name="StatusId">The Wayd status; null when the source's status has no match.</param>
/// <param name="StatusName">The status name as the source recorded it.</param>
/// <param name="StatusCategory">The status's category in the work type's workflow; null when it has no match.</param>
/// <param name="WorkTypeId">The Wayd work type; null when the source's type has no match in the workspace's process.</param>
/// <param name="WorkTypeName">The work type name as the source recorded it.</param>
/// <param name="TeamKey">The source's own value for a team recorded on the item; null for a source whose team comes from the iteration.</param>
/// <param name="AssignedToId">The employee the item was assigned to; null when unassigned or the identity is unmapped.</param>
/// <param name="AssignedToExternalId">The source's identity id for the assignee, which an admin's later mapping repoints from.</param>
/// <param name="StoryPoints">The story points estimate.</param>
/// <param name="Effort">The effort estimate.</param>
/// <param name="Size">The size estimate.</param>
public sealed record WorkItemTrackedState(
    Guid? IterationId,
    int? ExternalIterationId,
    int? StatusId,
    string StatusName,
    WorkStatusCategory? StatusCategory,
    int? WorkTypeId,
    string WorkTypeName,
    string? TeamKey,
    Guid? AssignedToId,
    string? AssignedToExternalId,
    double? StoryPoints,
    double? Effort,
    double? Size)
{
    /// <summary>
    /// Whether <paramref name="other"/> recorded the same values in the source. Only source values
    /// are compared: a resolved id can differ for the same source value when a mapping changed
    /// between syncs, and that is not a change to the work item.
    /// </summary>
    public bool HasSameSourceValues(WorkItemTrackedState other) =>
        ExternalIterationId == other.ExternalIterationId
        && StatusName == other.StatusName
        && WorkTypeName == other.WorkTypeName
        && TeamKey == other.TeamKey
        && AssignedToExternalId == other.AssignedToExternalId
        && StoryPoints == other.StoryPoints
        && Effort == other.Effort
        && Size == other.Size;
}
