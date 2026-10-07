using NodaTime;
using Wayd.Common.Domain.Enums.Work;

namespace Wayd.Work.Domain.Models.SprintScope;

/// <summary>
/// One period of a work item's history, reduced to what sprint scope reads. See
/// <see cref="WorkItemStateHistory"/> for how periods are built.
/// </summary>
/// <param name="IsRequirement">
/// Whether the period's work type is in the requirement tier. Scope counts only requirement-tier work, so a
/// change of type out of the tier leaves the sprint and one into it enters.
/// </param>
public sealed record SprintScopePeriod(
    Guid WorkItemId,
    Instant ValidFrom,
    Instant? ValidTo,
    Guid? IterationId,
    bool IsRequirement,
    WorkStatusCategory? StatusCategory,
    double? StoryPoints,
    double? Effort,
    double? Size)
{
    /// <summary>Whether the period holds at <paramref name="instant"/>.</summary>
    public bool Covers(Instant instant) => ValidFrom <= instant && (ValidTo is null || ValidTo > instant);
}
