namespace Wayd.Work.Domain.Models;

/// <summary>The tracked fields' values at one revision, as the source recorded them.</summary>
/// <param name="ExternalIterationId">The source's stable id for the iteration; null when the item was in none.</param>
/// <param name="StatusName">The status name.</param>
/// <param name="WorkTypeName">The work type name.</param>
/// <param name="TeamKey">The source's own value for a team recorded on the item.</param>
/// <param name="AssignedToExternalId">The source's identity id for the assignee.</param>
/// <param name="StoryPoints">The story points estimate.</param>
/// <param name="Effort">The effort estimate.</param>
/// <param name="Size">The size estimate.</param>
public sealed record WorkItemSourceValues(
    int? ExternalIterationId,
    string StatusName,
    string WorkTypeName,
    string? TeamKey,
    string? AssignedToExternalId,
    double? StoryPoints,
    double? Effort,
    double? Size);
