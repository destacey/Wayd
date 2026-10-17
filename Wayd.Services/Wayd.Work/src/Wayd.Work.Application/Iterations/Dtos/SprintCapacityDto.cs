using Wayd.Common.Domain.Enums.Organization;
using Wayd.Work.Domain.Models.SprintCapacity;

namespace Wayd.Work.Application.Iterations.Dtos;

/// <summary>
/// A sprint's capacity over its planned dates, in person-days, beside its scope and what that capacity is
/// expected to deliver. Worked out when read from the team's allocations and recent sprints; never stored.
/// </summary>
public sealed record SprintCapacityDto
{
    public Guid SprintId { get; init; }

    /// <summary>The first planned day of the sprint, which the capacity is counted from.</summary>
    public LocalDate Start { get; init; }

    /// <summary>The last planned day of the sprint, which the capacity is counted to.</summary>
    public LocalDate End { get; init; }

    /// <summary>The time allocated to the team over the sprint's planned dates in every role, in person-days, after holidays and time away.</summary>
    public decimal AllocatedPersonDays { get; init; }

    /// <summary>The part of it in roles that count toward delivery capacity, in person-days. What the scope is set against.</summary>
    public decimal DeliveryPersonDays { get; init; }

    /// <summary>The allocated time that people's time away takes, in person-days; already left out of the other two.</summary>
    public decimal AwayPersonDays { get; init; }

    /// <summary>
    /// Whether anyone is allocated to the team on any of the sprint's planned dates, whatever holidays, time away
    /// and team days off leave of it. False is a team that does not plan capacity, rather than one whose people
    /// are all away.
    /// </summary>
    public bool HasAllocations { get; init; }

    /// <summary>The estimate <c>scope</c> and <c>expectedScope</c> are in: the sprint's sizing method.</summary>
    public SizingMethod SizingMethod { get; init; }

    /// <summary>Which scope is set against the capacity: the work planned so far before the commitment point, the committed work after it.</summary>
    public SprintCapacityScopeBasis ScopeBasis { get; init; }

    /// <summary>The scope, by count under Count and otherwise in <c>sizingMethod</c>. An item with no estimate adds nothing.</summary>
    public double Scope { get; init; }

    /// <summary>Whether the scope could be compared with what the capacity is expected to deliver, and if not, why.</summary>
    public SprintCapacityOutcome Outcome { get; init; }

    /// <summary>
    /// What the capacity is expected to deliver, in the scope's unit: the team's velocity per person-day of
    /// delivery capacity over its recent completed sprints, times this sprint's. Null unless compared.
    /// </summary>
    /// <remarks>
    /// The recent sprints are those the team's sprint metrics count now, ended no later than this sprint. For a
    /// sprint some way back, few or none of them qualify, so its expectation draws on fewer sprints than the
    /// team had then, or there is none.
    /// </remarks>
    public double? ExpectedScope { get; init; }

    /// <summary>How many completed sprints <c>expectedScope</c> was drawn from.</summary>
    public int PriorSprints { get; init; }

    /// <summary>Whether the scope is more than <c>expectedScope</c>. Never true for a completed sprint.</summary>
    public bool IsOverCapacity { get; init; }
}
