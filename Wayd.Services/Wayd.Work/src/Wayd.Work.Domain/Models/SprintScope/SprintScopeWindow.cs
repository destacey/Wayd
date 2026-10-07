using NodaTime;

namespace Wayd.Work.Domain.Models.SprintScope;

/// <summary>
/// The instants a sprint's scope is measured between, and what tells carried-over work from descoped work.
/// </summary>
/// <param name="SprintId">The sprint whose scope is measured.</param>
/// <param name="Start">The commitment point: what is in the sprint here is committed.</param>
/// <param name="End">The effective end: what is in the sprint here is completed or carried over.</param>
/// <param name="LastDay">
/// The start of the sprint's last day: the earlier of its last planned day and the day it actually ended, in
/// its team's zone. Unfinished work moved to the team's next sprint from here on was carried over; before it,
/// it was descoped. The day it actually ended counts so that a team which plans the next sprint early — on the
/// Friday before a Monday holiday — carries its work over rather than descoping it.
/// </param>
/// <param name="NextSprintId">The team's next sprint; null for its last sprint or a sprint with no team.</param>
/// <param name="StartIsActual">Whether <paramref name="Start"/> is the team's recorded start rather than the default.</param>
/// <param name="EndIsActual">Whether <paramref name="End"/> is the team's recorded completion rather than the default.</param>
/// <param name="TimeZone">The zone the sprint's days are counted in.</param>
public sealed record SprintScopeWindow(
    Guid SprintId,
    Instant Start,
    Instant End,
    Instant LastDay,
    Guid? NextSprintId,
    bool StartIsActual,
    bool EndIsActual,
    DateTimeZone TimeZone)
{
    /// <summary>The window of a sprint on its team's timeline.</summary>
    public static SprintScopeWindow For(TeamSprintTimeline timeline, Iteration sprint)
    {
        var zone = timeline.ScheduleFor(sprint).TimeZone;
        var end = timeline.EffectiveEnd(sprint);

        return new SprintScopeWindow(
            sprint.Id,
            timeline.EffectiveStart(sprint),
            end,
            LastDayOf(sprint, end, zone),
            timeline.Next(sprint)?.Id,
            sprint.Started is not null,
            sprint.Completed is not null,
            zone);
    }

    /// <summary>
    /// The window of a sprint with no team, under the system default <paramref name="schedule"/>: its
    /// commitment is taken after the default grace period, it ends with its last planned day, and no work is
    /// carried over by moving it, since there is no team to say which sprint is next.
    /// </summary>
    public static SprintScopeWindow Unscheduled(Iteration sprint, SprintSchedule schedule)
    {
        var zone = schedule.TimeZone;
        var plannedStart = sprint.DateRange.Start ?? throw new InvalidOperationException($"Sprint {sprint.Id} has no planned start.");

        var start = sprint.Started ?? plannedStart.PlusDays(schedule.CommitmentGraceDays).AtStartOfDayInZone(zone).ToInstant();
        var end = sprint.Completed ?? PlannedLastDay(sprint).PlusDays(1).AtStartOfDayInZone(zone).ToInstant();

        return new SprintScopeWindow(
            sprint.Id,
            start,
            end,
            LastDayOf(sprint, end, zone),
            null,
            sprint.Started is not null,
            sprint.Completed is not null,
            zone);
    }

    private static Instant LastDayOf(Iteration sprint, Instant end, DateTimeZone zone)
    {
        var plannedLastDay = PlannedLastDay(sprint).AtStartOfDayInZone(zone).ToInstant();

        // The day holding the end's last moment: an end at midnight closes the day before it.
        var endedOn = end.Minus(Duration.Epsilon).InZone(zone).Date.AtStartOfDayInZone(zone).ToInstant();

        return endedOn < plannedLastDay ? endedOn : plannedLastDay;
    }

    private static LocalDate PlannedLastDay(Iteration sprint) =>
        sprint.DateRange.End ?? throw new InvalidOperationException($"Sprint {sprint.Id} has no planned end.");
}
