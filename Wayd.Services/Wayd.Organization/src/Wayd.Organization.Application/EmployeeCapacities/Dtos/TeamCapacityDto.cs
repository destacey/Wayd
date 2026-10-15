using NodaTime;
using Wayd.Common.Application.Dtos;

namespace Wayd.Organization.Application.EmployeeCapacities.Dtos;

/// <summary>
/// A team's capacity over a date range, from the time people are allocated to it, the days it works, and when
/// each of them is there. Capacity is in person-days: one person allocated in full, and there, for one working day.
/// </summary>
/// <remarks>
/// Every person-day figure is what is left after the holidays each person takes and their time away.
/// </remarks>
public sealed record TeamCapacityDto
{
    /// <summary>The first day of the range.</summary>
    public required LocalDate From { get; set; }

    /// <summary>The last day of the range.</summary>
    public required LocalDate To { get; set; }

    /// <summary>
    /// The team's working days in the range: days in its working week that are not holidays in its calendar.
    /// Each person's person-days follow the calendar assigned to them, or the system default, so they may be
    /// there on more or fewer of them.
    /// </summary>
    public required int WorkingDays { get; set; }

    /// <summary>The time allocated to the team in every role that people are there for, in person-days.</summary>
    public required decimal AllocatedPersonDays { get; set; }

    /// <summary>The part of the allocated time in roles that count toward delivery capacity, in person-days.</summary>
    public required decimal DeliveryPersonDays { get; set; }

    /// <summary>
    /// The time allocated to the team that people's time away takes, in person-days. Already left out of
    /// <see cref="AllocatedPersonDays"/>.
    /// </summary>
    public required decimal AwayPersonDays { get; set; }

    /// <summary>Whether the caller may change this team's allocations.</summary>
    public bool CanManage { get; set; }

    /// <summary>
    /// How many hours a working day has, from the system settings, so person-days and a share of a day can be read
    /// in hours.
    /// </summary>
    public required decimal WorkingDayHours { get; set; }

    /// <summary>
    /// The allocated time by team member role, by role name with the time in no role last. The lines add up to
    /// <see cref="AllocatedPersonDays"/>.
    /// </summary>
    public required IReadOnlyList<TeamCapacityRoleDto> Roles { get; set; }

    /// <summary>Everyone with time allocated to the team in the range, by name.</summary>
    public required IReadOnlyList<TeamCapacityPersonDto> People { get; set; }
}

/// <summary>The time allocated to a team in one team member role over a date range.</summary>
public sealed record TeamCapacityRoleDto
{
    /// <summary>The team member role, or null for time allocated in no role.</summary>
    public NavigationDto? Role { get; set; }

    /// <summary>Whether the role's time adds to the team's delivery capacity. True for time in no role.</summary>
    public required bool CountsTowardDeliveryCapacity { get; set; }

    /// <summary>The time allocated in the role, in person-days.</summary>
    public required decimal PersonDays { get; set; }
}

/// <summary>One person's time on a team over a date range.</summary>
public sealed record TeamCapacityPersonDto
{
    public required NavigationDto Employee { get; set; }

    /// <summary>Their time allocated to the team in every role, in person-days.</summary>
    public required decimal AllocatedPersonDays { get; set; }

    /// <summary>The part of it in roles that count toward delivery capacity, in person-days.</summary>
    public required decimal DeliveryPersonDays { get; set; }

    /// <summary>Their time allocated to other teams on this team's working days, in person-days.</summary>
    public required decimal ElsewherePersonDays { get; set; }

    /// <summary>
    /// The time allocated to the team that their time away takes, in person-days. Already left out of
    /// <see cref="AllocatedPersonDays"/>.
    /// </summary>
    public required decimal AwayPersonDays { get; set; }

    /// <summary>Their time away that falls in the range, in date order: when, and how much of each day.</summary>
    public required IReadOnlyList<TeamCapacityTimeAwayDto> TimeAway { get; set; }

    /// <summary>Their allocations to the team that fall in the range, in date order.</summary>
    public required IReadOnlyList<TeamCapacityAllocationDto> Allocations { get; set; }

    /// <summary>
    /// Their allocations to other teams that fall in the range, in date order: what
    /// <see cref="ElsewherePersonDays"/> is made of, and where time can be moved from.
    /// </summary>
    public required IReadOnlyList<EmployeeAllocationDto> ElsewhereAllocations { get; set; }
}

/// <summary>
/// A stretch of days a person on the team is away, as their teammates see it: when and how much of each day,
/// never why.
/// </summary>
public sealed record TeamCapacityTimeAwayDto
{
    /// <summary>The first day away, which may be before the range.</summary>
    public required LocalDate Start { get; set; }

    /// <summary>The last day away, which may be after the range.</summary>
    public required LocalDate End { get; set; }

    /// <summary>The share of each day the person is away, from 1 to 100.</summary>
    public required int PercentAway { get; set; }
}

/// <summary>An allocation of a person's time to the team.</summary>
public sealed record TeamCapacityAllocationDto
{
    public required Guid Id { get; set; }

    /// <summary>The team member role the time is spent in, or null when none was chosen.</summary>
    public NavigationDto? Role { get; set; }

    /// <summary>Whether the allocation adds to the team's delivery capacity. True when it has no role.</summary>
    public required bool CountsTowardDeliveryCapacity { get; set; }

    /// <summary>The first day of the allocation, which may be before the range.</summary>
    public required LocalDate Start { get; set; }

    /// <summary>The last day of the allocation, or null when it has no end. It may be after the range.</summary>
    public LocalDate? End { get; set; }

    /// <summary>The share of the person's time, from 1 to 100.</summary>
    public required int Percent { get; set; }

    /// <summary>
    /// What the allocation is worth over the team's working days in the range, in person-days, after the
    /// holidays the person takes and their time away.
    /// </summary>
    public required decimal PersonDays { get; set; }
}
