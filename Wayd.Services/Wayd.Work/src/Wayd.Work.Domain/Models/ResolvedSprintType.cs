using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// A sprint's type as worked out when read, and the rule that decided it. Not stored: changing the mapped
/// planning interval iteration's category, or the sprint's mapping, reclassifies the sprint on the next read.
/// </summary>
public readonly record struct ResolvedSprintType(SprintType Type, SprintTypeSource Source)
{
    /// <summary>
    /// The type the team set, otherwise the one <paramref name="mappedCategory"/> declares, otherwise standard.
    /// For queries that project the override rather than load the sprint.
    /// </summary>
    public static ResolvedSprintType From(SprintType? sprintTypeOverride, IterationCategory? mappedCategory) =>
        (sprintTypeOverride, mappedCategory) switch
        {
            ({ } set, _) => new(set, SprintTypeSource.Team),
            (null, { } category) => new(category.ToSprintType(), SprintTypeSource.PlanningInterval),
            _ => new(SprintType.Standard, SprintTypeSource.Default),
        };
}
