using NodaTime;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Work.Domain.Models;

/// <summary>What a dependency reads from one of its ends: the work item's status and the plan it is still on.</summary>
public sealed record DependencyWorkItemInfo
{
    /// <summary>The work item at this end of the dependency.</summary>
    public Guid WorkItemId { get; set; }

    /// <summary>The work item's status category, which decides the dependency's state and health.</summary>
    public StatusCategory StatusCategory { get; set; }


    /// <summary>
    /// The last planned day of the work item's sprint, while that day is not yet over in the sprint team's time
    /// zone. Null when the item is in no sprint or its sprint has ended, which both count as no plan.
    /// </summary>
    public LocalDate? PlannedOn { get; set; }

    /// <summary>The dependency info of a work item in no sprint, which has no plan.</summary>
    public static DependencyWorkItemInfo Create(Guid workItemId, StatusCategory statusCategory) => new()
    {
        WorkItemId = workItemId,
        StatusCategory = statusCategory,
    };

    /// <summary>
    /// The dependency info at <paramref name="now"/> of a work item in a sprint. The sprint has ended once its
    /// last planned day is over in <paramref name="sprintZone"/>, matching <see cref="TeamSprintTimeline.PlannedEnd"/>.
    /// </summary>
    /// <param name="sprintEnd">The last planned day of the item's sprint.</param>
    /// <param name="sprintZone">The zone the sprint's days are counted in: its team's on the sprint's planned
    /// start, or the system time zone for a sprint with no team or no planned start.</param>
    public static DependencyWorkItemInfo Create(Guid workItemId, StatusCategory statusCategory, LocalDate sprintEnd, DateTimeZone sprintZone, Instant now)
    {
        var ended = now >= sprintEnd.PlusDays(1).AtStartOfDayInZone(sprintZone).ToInstant();

        return new DependencyWorkItemInfo
        {
            WorkItemId = workItemId,
            StatusCategory = statusCategory,
            PlannedOn = ended ? null : sprintEnd
        };
    }
}
