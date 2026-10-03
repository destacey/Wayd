using NodaTime;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// The time zone a sprint's planned days are counted in, and how many days after its planned start the
/// team's commitment is taken when the team does not start the sprint itself (1 is the end of the first
/// planned day).
/// </summary>
public sealed record SprintSchedule(DateTimeZone TimeZone, int CommitmentGraceDays);

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
