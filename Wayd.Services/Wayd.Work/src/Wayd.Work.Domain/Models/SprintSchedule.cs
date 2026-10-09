using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Organizations;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// The time zone a sprint's planned days are counted in, how many days after its planned start the team's
/// commitment is taken when the team does not start the sprint itself (1 is the end of the first planned
/// day), which of a work item's estimates the sprint is measured in, and which days the team works.
/// </summary>
public sealed record SprintSchedule(DateTimeZone TimeZone, int CommitmentGraceDays, SizingMethod SizingMethod)
{
    /// <summary>The days of the week the team works.</summary>
    public WorkingWeek WorkingWeek { get; init; } = WorkingWeek.MondayToFriday;

    /// <summary>The team's holiday calendar, or null for the system default calendar.</summary>
    public Guid? HolidayCalendarId { get; init; }
}

/// <summary>A schedule and the days it was in effect for a team, end inclusive.</summary>
public sealed record SprintSchedulePeriod(LocalDate Start, LocalDate? End, SprintSchedule Schedule);

/// <summary>
/// The schedules a team has had over time. A sprint takes the one in effect on its planned start and keeps
/// it for the whole sprint; a day the team had none takes <see cref="Fallback"/>, the system defaults.
/// </summary>
public sealed class TeamSprintSchedules(IEnumerable<SprintSchedulePeriod> periods, SprintSchedule fallback)
{
    private readonly List<SprintSchedulePeriod> _periods = [.. periods];

    public SprintSchedule Fallback { get; } = fallback;

    public SprintSchedule AsOf(LocalDate date) =>
        _periods.FirstOrDefault(p => p.Start <= date && (p.End is null || p.End >= date))?.Schedule ?? Fallback;
}
