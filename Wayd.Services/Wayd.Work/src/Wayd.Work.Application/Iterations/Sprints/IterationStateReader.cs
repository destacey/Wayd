using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Models.Planning.Iterations;

namespace Wayd.Work.Application.Iterations.Sprints;

/// <summary>
/// Works out iteration states at one moment. A sprint in its team's timeline follows its actual and default
/// dates in the team's zone; any other iteration — no mapped team, not a sprint, or missing a planned date —
/// follows its planned days in the system default zone.
/// </summary>
public sealed class IterationStateReader
{
    private readonly Dictionary<Guid, (TeamSprintTimeline Timeline, Iteration Sprint)> _sprints;
    private readonly LocalDate _defaultToday;

    public IterationStateReader(IEnumerable<TeamSprintTimeline> timelines, DateTimeZone defaultZone, Instant now)
    {
        Now = now;
        _defaultToday = now.InZone(defaultZone).Date;
        _sprints = timelines
            .SelectMany(t => t.Sprints.Select(s => (Timeline: t, Sprint: s)))
            .ToDictionary(e => e.Sprint.Id);
    }

    public Instant Now { get; }

    /// <param name="id">Matched to the timelines by id, so the caller's copy may come from any read.</param>
    /// <param name="plannedDays">Used only when the iteration is in none of the timelines.</param>
    public IterationState StateOf(Guid id, IterationDateRange plannedDays) =>
        _sprints.TryGetValue(id, out var entry)
            ? entry.Timeline.StateAt(entry.Sprint, Now)
            : plannedDays.StateOn(_defaultToday);

    public IterationState StateOf(Iteration iteration) => StateOf(iteration.Id, iteration.DateRange);

    /// <summary>
    /// When the sprint is Active, end exclusive, and the zone its days are counted in. Null when it is in none
    /// of the timelines and only has planned days.
    /// </summary>
    public (Instant From, Instant Until, DateTimeZone TimeZone)? ActivePeriodOf(Guid id) =>
        _sprints.TryGetValue(id, out var entry)
            ? (entry.Timeline.ActiveFrom(entry.Sprint), entry.Timeline.EffectiveEnd(entry.Sprint), entry.Timeline.ScheduleFor(entry.Sprint).TimeZone)
            : null;
}
