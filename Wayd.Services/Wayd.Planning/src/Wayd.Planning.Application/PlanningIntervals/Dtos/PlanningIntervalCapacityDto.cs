using Wayd.Common.Application.Dtos;
using Wayd.Planning.Application.Models;

namespace Wayd.Planning.Application.PlanningIntervals.Dtos;

/// <summary>
/// The capacity of each team in a planning interval, for the whole planning interval and for each of its
/// iterations by the planning interval's own dates, whatever a team's sprint length or cadence. Worked out when
/// read from the teams' allocations; never stored.
/// </summary>
public sealed record PlanningIntervalCapacityDto
{
    /// <summary>The planning interval's first day.</summary>
    public required LocalDate Start { get; init; }

    /// <summary>The planning interval's last day.</summary>
    public required LocalDate End { get; init; }

    /// <summary>The planning interval's iterations, in date order: the columns each team's capacity is read over.</summary>
    public required IReadOnlyList<PlanningIntervalCapacityIterationDto> Iterations { get; init; }

    /// <summary>
    /// The teams in the planning interval, teams before teams of teams and then by code. A team of teams is
    /// counted as its rollup, so a team beneath it that is also in the planning interval is counted in both rows.
    /// </summary>
    public required IReadOnlyList<PlanningIntervalTeamCapacityDto> Teams { get; init; }
}

/// <summary>An iteration of a planning interval, as a capacity column.</summary>
public sealed record PlanningIntervalCapacityIterationDto
{
    public required Guid Id { get; init; }

    /// <summary>The iteration's key, an alternate to its id.</summary>
    public required int Key { get; init; }

    public required string Name { get; init; }

    /// <summary>The iteration's category, such as Development or Innovation and Planning.</summary>
    public required SimpleNavigationDto Category { get; init; }

    /// <summary>The iteration's first day.</summary>
    public required LocalDate Start { get; init; }

    /// <summary>The iteration's last day.</summary>
    public required LocalDate End { get; init; }
}

/// <summary>One team's capacity across a planning interval.</summary>
public sealed record PlanningIntervalTeamCapacityDto
{
    public required PlanningTeamNavigationDto Team { get; init; }

    /// <summary>Whether the team is active; an inactive team's allocations can no longer be added to.</summary>
    public required bool IsActive { get; init; }

    /// <summary>
    /// Whether anyone is allocated to the team, or to a team beneath a team of teams, at any time in the planning
    /// interval. A team with no one has nothing to plan with, rather than no time.
    /// </summary>
    public required bool HasAllocations { get; init; }

    /// <summary>The team's capacity over the whole planning interval.</summary>
    public required PlanningIntervalCapacityFiguresDto Total { get; init; }

    /// <summary>The team's capacity in each iteration, in the same order as the planning interval's <c>iterations</c>.</summary>
    public required IReadOnlyList<PlanningIntervalCapacityFiguresDto> Iterations { get; init; }

    /// <summary>
    /// The team's sprint days off in the planning interval, in date order: days the whole team is off, already
    /// left out of its capacity. Empty for a team of teams, whose teams' days off are left out of its rollup.
    /// </summary>
    public required IReadOnlyList<LocalDate> TeamDaysOff { get; init; }
}

/// <summary>A team's capacity over a run of days, in person-days, after holidays, time away and team days off.</summary>
public sealed record PlanningIntervalCapacityFiguresDto
{
    /// <summary>The time allocated in every role.</summary>
    public required decimal AllocatedPersonDays { get; init; }

    /// <summary>The part of it in roles that count toward delivery capacity.</summary>
    public required decimal DeliveryPersonDays { get; init; }

    /// <summary>The allocated time that people's time away takes, already left out of the other two.</summary>
    public required decimal AwayPersonDays { get; init; }
}
