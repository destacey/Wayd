using NodaTime;
using Wayd.Common.Application.Dtos;

namespace Wayd.Organization.Application.EmployeeCapacities.Dtos;

/// <summary>
/// A team's capacity over a date range day by day, to plan against: the capacity totals, and for each person
/// their time in each role on each of the team's working days, beside what else decides it that day.
/// </summary>
/// <remarks>
/// Nothing here is stored per day. Each day is read from the allocations, which are percents over date ranges,
/// and a change to a day or week is an allocation change over that range.
/// </remarks>
public sealed record TeamCapacityPlanDto
{
    /// <summary>The team's capacity over the range, as the team's capacity read gives it.</summary>
    public required TeamCapacityDto Capacity { get; set; }

    /// <summary>
    /// The days of the team's working week in the range, in date order, holidays included. The person-days of
    /// each day already leave out the holidays each person takes.
    /// </summary>
    public required IReadOnlyList<LocalDate> Days { get; set; }

    /// <summary>Everyone with time allocated to the team in the range, in the order of <see cref="TeamCapacityDto.People"/>.</summary>
    public required IReadOnlyList<TeamCapacityPlanPersonDto> People { get; set; }
}

/// <summary>One person's time on a team, day by day.</summary>
public sealed record TeamCapacityPlanPersonDto
{
    /// <summary>The employee, as named in <see cref="TeamCapacityDto.People"/>.</summary>
    public required Guid EmployeeId { get; set; }

    /// <summary>
    /// Whether the caller may record, change and remove this person's time away: the person, anyone they report up
    /// to, and a time away administrator.
    /// </summary>
    public bool CanManageTimeAway { get; set; }

    /// <summary>
    /// The working days on which something besides their allocation here decides their time: a holiday, time
    /// away, or time allocated to other teams. A day left out has none of these.
    /// </summary>
    public required IReadOnlyList<TeamCapacityPlanPersonDayDto> Days { get; set; }

    /// <summary>Their time in each team member role, by role name with the time in no role last.</summary>
    public required IReadOnlyList<TeamCapacityPlanRowDto> Rows { get; set; }
}

/// <summary>What else decides a person's time on one of the team's working days.</summary>
public sealed record TeamCapacityPlanPersonDayDto
{
    public required LocalDate Date { get; set; }

    /// <summary>Whether the day is a holiday in the calendar the person takes.</summary>
    public required bool IsHoliday { get; set; }

    /// <summary>The share of the day the person is away, from 0 to 100. Never why.</summary>
    public required int PercentAway { get; set; }

    /// <summary>The share of the person's time allocated to other teams that day, from 0 to 100.</summary>
    public required int ElsewherePercent { get; set; }
}

/// <summary>A person's time on a team in one team member role, day by day.</summary>
public sealed record TeamCapacityPlanRowDto
{
    /// <summary>The team member role, or null for time allocated in no role.</summary>
    public NavigationDto? Role { get; set; }

    /// <summary>Whether the role's time adds to the team's delivery capacity. True for time in no role.</summary>
    public required bool CountsTowardDeliveryCapacity { get; set; }

    /// <summary>The working days with time allocated in the role, in date order. A day left out has none.</summary>
    public required IReadOnlyList<TeamCapacityPlanDayDto> Days { get; set; }
}

/// <summary>A person's time in one role on one of the team's working days.</summary>
public sealed record TeamCapacityPlanDayDto
{
    public required LocalDate Date { get; set; }

    /// <summary>The share of the person's time allocated, from 1 to 100.</summary>
    public required int Percent { get; set; }

    /// <summary>What it is worth, in person-days, after the holidays the person takes and their time away.</summary>
    public required decimal PersonDays { get; set; }
}
