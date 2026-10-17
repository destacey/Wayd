using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Organization.Application.Models;

namespace Wayd.Organization.Application.EmployeeCapacities.Dtos;

/// <summary>
/// A team of teams' capacity over a date range with the teams beneath it rolled up: the time allocated to it
/// directly, plus the time allocated to each team beneath it on the days that team was in the hierarchy.
/// Capacity is in person-days: one person allocated in full for one working day of the team they are allocated to.
/// </summary>
public sealed record TeamCapacityRollupDto
{
    /// <summary>The first day of the range.</summary>
    public required LocalDate From { get; set; }

    /// <summary>The last day of the range.</summary>
    public required LocalDate To { get; set; }

    /// <summary>The time allocated to the team of teams and to every team beneath it, in person-days.</summary>
    public required decimal AllocatedPersonDays { get; set; }

    /// <summary>The part of <c>allocatedPersonDays</c> in roles that count toward delivery capacity.</summary>
    public required decimal DeliveryPersonDays { get; set; }

    /// <summary>The time allocated to the team of teams itself, in person-days.</summary>
    public required decimal DirectAllocatedPersonDays { get; set; }

    /// <summary>The part of <c>directAllocatedPersonDays</c> in roles that count toward delivery capacity.</summary>
    public required decimal DirectDeliveryPersonDays { get; set; }

    /// <summary>
    /// How many different people have an allocation to the team of teams or to a team beneath it on the days it
    /// counts. Someone allocated at more than one level is one person. An allocation that falls on none of
    /// the team's working days still counts its person, at no person-days.
    /// </summary>
    public required int People { get; set; }

    /// <summary>
    /// The allocated time across the team of teams and every team beneath it by team member role, by role name
    /// with the time in no role last. The lines add up to <c>allocatedPersonDays</c>.
    /// </summary>
    public required IReadOnlyList<TeamCapacityRoleDto> Roles { get; set; }

    /// <summary>The teams directly beneath the team of teams in the range, by name.</summary>
    public required IReadOnlyList<TeamCapacityRollupNodeDto> Children { get; set; }
}

/// <summary>
/// The capacity of a team or team of teams beneath the one a rollup is for, over the days it was in that hierarchy.
/// </summary>
public sealed record TeamCapacityRollupNodeDto
{
    /// <summary>The team or team of teams this node is for.</summary>
    public required TeamNavigationDto Team { get; set; }

    /// <summary>
    /// The days of the range the team counts for, in date order: those its membership of its parent covers,
    /// less any its parent was itself out of the hierarchy.
    /// </summary>
    public required IReadOnlyList<TeamCapacityPeriodDto> Periods { get; set; }

    /// <summary>
    /// The team's own working days within <c>periods</c>. A team of teams has the system default
    /// working week and holidays.
    /// </summary>
    public required int WorkingDays { get; set; }

    /// <summary>The time allocated to the team and to every team beneath it, in person-days.</summary>
    public required decimal AllocatedPersonDays { get; set; }

    /// <summary>The part of <c>allocatedPersonDays</c> in roles that count toward delivery capacity.</summary>
    public required decimal DeliveryPersonDays { get; set; }

    /// <summary>The time allocated to the team itself, in person-days.</summary>
    public required decimal DirectAllocatedPersonDays { get; set; }

    /// <summary>The part of <c>directAllocatedPersonDays</c> in roles that count toward delivery capacity.</summary>
    public required decimal DirectDeliveryPersonDays { get; set; }

    /// <summary>
    /// Everyone with an allocation to the team itself within <c>periods</c>, by name, including anyone
    /// whose allocation falls on none of its working days there.
    /// </summary>
    public required IReadOnlyList<TeamCapacityRollupPersonDto> People { get; set; }

    /// <summary>The teams directly beneath this one within <c>periods</c>, by name. Empty for a team.</summary>
    public required IReadOnlyList<TeamCapacityRollupNodeDto> Children { get; set; }
}

/// <summary>A run of days a team counts toward a rollup, both inclusive.</summary>
public sealed record TeamCapacityPeriodDto
{
    /// <summary>The first day of the run.</summary>
    public required LocalDate Start { get; set; }

    /// <summary>The last day of the run.</summary>
    public required LocalDate End { get; set; }
}

/// <summary>One person's time on a team beneath the one a rollup is for.</summary>
public sealed record TeamCapacityRollupPersonDto
{
    /// <summary>The person allocated.</summary>
    public required NavigationDto Employee { get; set; }

    /// <summary>Their time allocated to the team in every role on the days it counts, in person-days.</summary>
    public required decimal AllocatedPersonDays { get; set; }

    /// <summary>The part of it in roles that count toward delivery capacity, in person-days.</summary>
    public required decimal DeliveryPersonDays { get; set; }

    /// <summary>
    /// Their allocations to the team that fall on the days it counts, in date order. Each one's
    /// <c>personDays</c> is over those days only.
    /// </summary>
    public required IReadOnlyList<TeamCapacityAllocationDto> Allocations { get; set; }
}
