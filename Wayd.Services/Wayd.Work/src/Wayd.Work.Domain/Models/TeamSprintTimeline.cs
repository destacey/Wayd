using System.Globalization;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events.Planning.Iterations;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// One team's sprints in planned order, and the rules that hold across them: when each one actually
/// starts and ends, and which of them may be started, completed or reopened.
/// </summary>
/// <remarks>
/// A sprint the team did not start or complete follows its schedule: it is Active from the start of its first
/// planned day to the end of its last, in the team's zone, and its commitment is taken at the end of the
/// commitment grace period after its planned start. Azure DevOps lets one team's sprints overlap and Wayd keeps
/// the source dates, so an end the team did not record is cut to the next sprint's start — its recorded start,
/// or else the start of its first planned day — which is what keeps the team's Active periods from overlapping.
/// </remarks>
public sealed class TeamSprintTimeline
{
    /// <summary>How many days before its planned start the team's first sprint may be started.</summary>
    public const int EarlyStartDays = 3;

    private readonly List<Iteration> _sprints;
    private readonly TeamSprintSchedules _schedules;

    public TeamSprintTimeline(Guid teamId, IEnumerable<Iteration> sprints, TeamSprintSchedules schedules)
    {
        TeamId = teamId;
        _schedules = schedules;
        _sprints = [.. sprints
            .Where(s => s.Type == IterationType.Sprint
                && s.TeamId == teamId
                && s.DateRange.Start.HasValue
                && s.DateRange.End.HasValue)
            .OrderBy(s => s.DateRange.Start)
            .ThenBy(s => s.Key)];
    }

    public Guid TeamId { get; }

    /// <summary>The system default zone, which a day the team had no schedule falls back to.</summary>
    public DateTimeZone DefaultZone => _schedules.Fallback.TimeZone;

    public IReadOnlyList<Iteration> Sprints => _sprints.AsReadOnly();

    /// <summary>The sprint the team started and has not completed, if any. A team has at most one.</summary>
    public Iteration? OpenSprint => _sprints.FirstOrDefault(s => s.Started is not null && s.Completed is null);

    public bool Contains(Iteration sprint) => _sprints.Contains(sprint);

    public SprintSchedule ScheduleFor(Iteration sprint) => _schedules.AsOf(PlannedStartDate(sprint));

    public Iteration? Previous(Iteration sprint)
    {
        var index = IndexOf(sprint);
        return index > 0 ? _sprints[index - 1] : null;
    }

    public Iteration? Next(Iteration sprint)
    {
        var index = IndexOf(sprint);
        return index < _sprints.Count - 1 ? _sprints[index + 1] : null;
    }

    /// <summary>When the team's commitment is taken if it does not start the sprint itself.</summary>
    public Instant DefaultStart(Iteration sprint)
    {
        var schedule = ScheduleFor(sprint);
        return PlannedStartDate(sprint).PlusDays(schedule.CommitmentGraceDays)
            .AtStartOfDayInZone(schedule.TimeZone)
            .ToInstant();
    }

    /// <summary>The end of the sprint's last planned day, in its team's zone.</summary>
    public Instant PlannedEnd(Iteration sprint)
    {
        var schedule = ScheduleFor(sprint);
        return PlannedEndDate(sprint).PlusDays(1)
            .AtStartOfDayInZone(schedule.TimeZone)
            .ToInstant();
    }

    /// <summary>The start of the sprint's first planned day, in its team's zone.</summary>
    public Instant PlannedStart(Iteration sprint) =>
        PlannedStartDate(sprint)
            .AtStartOfDayInZone(ScheduleFor(sprint).TimeZone)
            .ToInstant();

    /// <summary>
    /// The sprint's commitment point: its actual start, or else <see cref="DefaultStart"/>. Metrics and the
    /// lifecycle windows count from it; whether the sprint is Active does not (see <see cref="ActiveFrom"/>).
    /// </summary>
    public Instant EffectiveStart(Iteration sprint) => sprint.Started ?? DefaultStart(sprint);

    /// <summary>
    /// When the sprint stops being Active: its actual completion, or else the end of its last planned day, cut
    /// to the next sprint's recorded start or first planned day where the source plans the two to overlap.
    /// </summary>
    public Instant EffectiveEnd(Iteration sprint)
    {
        if (sprint.Completed is { } completed)
            return completed;

        var plannedEnd = PlannedEnd(sprint);
        var next = Next(sprint);
        if (next is null)
            return plannedEnd;

        // Not the next sprint's ActiveFrom, which is itself bounded by this end.
        var nextStart = next.Started ?? PlannedStart(next);
        return nextStart < plannedEnd ? nextStart : plannedEnd;
    }

    /// <summary>
    /// When the sprint shows as Active: its actual start, or else the start of its first planned day in the
    /// team's zone. That is earlier than <see cref="DefaultStart"/>, which leaves the team the grace period to
    /// plan before its commitment is taken. Never before the previous sprint's effective end, so a team has at
    /// most one Active sprint.
    /// </summary>
    public Instant ActiveFrom(Iteration sprint)
    {
        if (sprint.Started is { } started)
            return started;

        var plannedStart = PlannedStart(sprint);

        return Previous(sprint) is { } previous && EffectiveEnd(previous) > plannedStart
            ? EffectiveEnd(previous)
            : plannedStart;
    }

    /// <summary>
    /// The sprint's state at <paramref name="now"/>: Future before <see cref="ActiveFrom"/>, Active until its
    /// effective end, and Completed after.
    /// </summary>
    public IterationState StateAt(Iteration sprint, Instant now)
    {
        if (now >= EffectiveEnd(sprint))
            return IterationState.Completed;

        return now < ActiveFrom(sprint) ? IterationState.Future : IterationState.Active;
    }

    /// <summary>Whether the sprint's planned days overlap the previous sprint's in the source system.</summary>
    public bool OverlapsPrevious(Iteration sprint) =>
        Previous(sprint) is { } previous && PlannedEndDate(previous) >= PlannedStartDate(sprint);

    /// <summary>Whether the sprint's planned days overlap the next sprint's in the source system.</summary>
    public bool OverlapsNext(Iteration sprint) =>
        Next(sprint) is { } next && PlannedEndDate(sprint) >= PlannedStartDate(next);

    /// <summary>
    /// The moments, up to <paramref name="now"/>, the sprint could be recorded as started. It starts after the
    /// previous sprint started and before the next one does, so the team moves on one sprint at a time; never
    /// before the previous sprint's recorded completion, nor before the open sprint's start, since starting
    /// completes that sprint at the same moment.
    /// </summary>
    public Result<InstantWindow> StartWindow(Iteration sprint, Instant now)
    {
        var eligible = CheckEligible(sprint);
        if (eligible.IsFailure)
            return Result.Failure<InstantWindow>(eligible.Error);

        if (sprint.Started is not null)
            return Result.Failure<InstantWindow>("The sprint has already been started.");

        if (HasLaterStarted(sprint))
            return Result.Failure<InstantWindow>("A later sprint for this team has already been started.");

        var previous = Previous(sprint);
        Instant earliest;
        if (previous is null)
        {
            earliest = PlannedStartDate(sprint).PlusDays(-EarlyStartDays)
                .AtStartOfDayInZone(ScheduleFor(sprint).TimeZone)
                .ToInstant();
        }
        else
        {
            earliest = EffectiveStart(previous).Plus(ExcludedStep);
            if (previous.Completed is { } previousCompleted && previousCompleted > earliest)
                earliest = previousCompleted;
        }

        if (OpenSprint is { Started: { } openStarted } open && open != sprint && openStarted.Plus(ExcludedStep) > earliest)
            earliest = openStarted.Plus(ExcludedStep);

        var plannedEnd = PlannedEnd(sprint);
        var latest = Min(now, plannedEnd.Minus(ExcludedStep));
        Instant? nextStart = Next(sprint) is { } next ? EffectiveStart(next) : null;
        if (nextStart is { } beforeNext)
            latest = Min(latest, beforeNext.Minus(ExcludedStep));

        if (earliest <= latest)
            return new InstantWindow(earliest, latest);

        if (nextStart <= earliest)
            return Result.Failure<InstantWindow>("Only the team's next sprint can be started.");

        if (plannedEnd <= earliest)
            return Result.Failure<InstantWindow>("The sprint's planned end has passed.");

        return previous is null
            ? Result.Failure<InstantWindow>($"The team's first sprint can be started at most {EarlyStartDays} days before its planned start.")
            : Result.Failure<InstantWindow>("The sprint can't be started until the previous sprint has started.");
    }

    /// <summary>Whether the sprint may be recorded as started at <paramref name="at"/>, deciding at <paramref name="now"/>.</summary>
    public Result CanStart(Iteration sprint, Instant at, Instant now)
    {
        var window = StartWindow(sprint, now);
        if (window.IsFailure)
            return Result.Failure(window.Error);

        if (at > now)
            return Result.Failure("A start can't be recorded in the future.");

        return window.Value.Contains(at)
            ? Result.Success()
            : Result.Failure($"The sprint can be started between {Describe(sprint, window.Value.Earliest)} and {Describe(sprint, window.Value.Latest)}.");
    }

    /// <summary>
    /// The moments, up to <paramref name="now"/>, the sprint could be recorded as completed: after its actual or
    /// default start.
    /// </summary>
    public Result<InstantWindow> CompleteWindow(Iteration sprint, Instant now)
    {
        var eligible = CheckEligible(sprint);
        if (eligible.IsFailure)
            return Result.Failure<InstantWindow>(eligible.Error);

        if (sprint.Completed is not null)
            return Result.Failure<InstantWindow>("The sprint has already been completed.");

        if (HasLaterStarted(sprint))
            return Result.Failure<InstantWindow>("A later sprint for this team has already been started, which ended this one.");

        if (_sprints.Take(IndexOf(sprint)).Any(s => s.Started is not null && s.Completed is null))
            return Result.Failure<InstantWindow>("An earlier sprint for this team is still open. Complete it first.");

        var earliest = EffectiveStart(sprint).Plus(ExcludedStep);
        if (earliest > now)
            return Result.Failure<InstantWindow>("The sprint can't be completed before it starts.");

        return new InstantWindow(earliest, now);
    }

    /// <summary>Whether the sprint may be recorded as completed at <paramref name="at"/>, deciding at <paramref name="now"/>.</summary>
    public Result CanComplete(Iteration sprint, Instant at, Instant now)
    {
        var window = CompleteWindow(sprint, now);
        if (window.IsFailure)
            return Result.Failure(window.Error);

        if (at > now)
            return Result.Failure("A completion can't be recorded in the future.");

        return window.Value.Contains(at)
            ? Result.Success()
            : Result.Failure($"The sprint can be completed between {Describe(sprint, window.Value.Earliest)} and {Describe(sprint, window.Value.Latest)}.");
    }

    public Result CanReopen(Iteration sprint)
    {
        var eligible = CheckEligible(sprint);
        if (eligible.IsFailure)
            return eligible;

        if (sprint.Completed is null)
            return Result.Failure("The sprint has not been completed.");

        if (HasLaterStarted(sprint))
            return Result.Failure("A later sprint for this team has already been started.");

        if (sprint.Started is not null && OpenSprint is not null)
            return Result.Failure("Another sprint for this team is open.");

        return Result.Success();
    }

    /// <summary>
    /// Checks that the team's sprints may take the actual dates in <paramref name="corrections"/>, all at once,
    /// deciding at <paramref name="now"/>, and returns them as the only form <see cref="Iteration.CorrectActualDates"/>
    /// accepts. A corrected start keeps the live start's bounds, and the resulting actual periods may not overlap: a
    /// completion can't pass the next sprint's actual or default start, and an open sprint can't be followed by one
    /// with recorded dates. Sprints the corrections leave as they are aren't rechecked, nor is a boundary neither
    /// side of which moves, so a quirk elsewhere in the team's history doesn't block a correction.
    /// </summary>
    public Result<TeamSprintCorrection> ValidateCorrection(IReadOnlyDictionary<Iteration, SprintActualDates> corrections, Instant now)
    {
        var allowed = CheckCorrection(corrections, now);
        return allowed.IsSuccess
            ? new TeamSprintCorrection(corrections)
            : Result.Failure<TeamSprintCorrection>(allowed.Error);
    }

    private Result CheckCorrection(IReadOnlyDictionary<Iteration, SprintActualDates> corrections, Instant now)
    {
        var corrected = corrections
            .Where(c => c.Value != ActualDates(c.Key))
            .ToDictionary(c => c.Key, c => c.Value);

        foreach (var sprint in corrected.Keys)
        {
            var eligible = CheckEligible(sprint);
            if (eligible.IsFailure)
                return eligible;
        }

        SprintActualDates Proposed(Iteration sprint) =>
            corrected.TryGetValue(sprint, out var dates) ? dates : ActualDates(sprint);

        Instant ProposedEffectiveStart(Iteration sprint) => Proposed(sprint).Started ?? DefaultStart(sprint);

        foreach (var (sprint, dates) in corrected.OrderBy(c => IndexOf(c.Key)))
        {
            if (dates.Started > now || dates.Completed > now)
                return Result.Failure($"{sprint.Name}: actual dates can't be in the future.");

            if (dates.Started is { } started)
            {
                var previous = Previous(sprint);
                var earliest = previous is null
                    ? PlannedStartDate(sprint).PlusDays(-EarlyStartDays).AtStartOfDayInZone(ScheduleFor(sprint).TimeZone).ToInstant()
                    : ProposedEffectiveStart(previous).Plus(ExcludedStep);
                var latest = PlannedEnd(sprint).Minus(ExcludedStep);

                if (started < earliest || started > latest)
                    return Result.Failure($"{sprint.Name} can be started between {Describe(sprint, earliest)} and {Describe(sprint, latest)}.");
            }

            if (dates.Completed is { } completed && completed <= ProposedEffectiveStart(sprint))
                return Result.Failure($"{sprint.Name} can't be completed before it starts, at {Describe(sprint, ProposedEffectiveStart(sprint))}.");
        }

        for (var i = 0; i < _sprints.Count; i++)
        {
            var sprint = _sprints[i];
            var dates = Proposed(sprint);

            // A sprint whose previous sprint's start moved later must still start after it.
            if (i > 0 && dates.Started is { } started && corrected.ContainsKey(_sprints[i - 1]) && !corrected.ContainsKey(sprint)
                && started <= ProposedEffectiveStart(_sprints[i - 1]))
            {
                return Result.Failure($"{_sprints[i - 1].Name} can't start after {sprint.Name} started, at {Describe(sprint, started)}.");
            }

            if (dates.Started is not null && dates.Completed is null
                && _sprints.Skip(i + 1).FirstOrDefault(s => Proposed(s) != NoActualDates) is { } laterRecorded
                && (corrected.ContainsKey(sprint) || corrected.ContainsKey(laterRecorded)))
            {
                return Result.Failure($"{sprint.Name} would still be open when {laterRecorded.Name} has actual dates. Complete it as well.");
            }

            // Checked only where the completion or the next start moves: the live Complete has no cap at the next
            // sprint's default start, so a correction elsewhere must not trip over a late completion it left alone.
            if (dates.Completed is { } completed && i < _sprints.Count - 1)
            {
                var next = _sprints[i + 1];
                var nextStart = ProposedEffectiveStart(next);
                var moved = completed != sprint.Completed || nextStart != EffectiveStart(next);
                if (moved && completed > nextStart)
                    return Result.Failure($"{sprint.Name} can't be completed after {next.Name} starts, at {Describe(next, nextStart)}. Correct both together.");
            }
        }

        return Result.Success();
    }

    private static readonly SprintActualDates NoActualDates = new(null, null);

    private static SprintActualDates ActualDates(Iteration sprint) => new(sprint.Started, sprint.Completed);

    // A bound that excludes a moment sits this far past it. A millisecond, not a tick: the API's clients
    // hold instants to the millisecond, so a tick-sized step would round back onto the excluded moment.
    private static readonly Duration ExcludedStep = Duration.FromMilliseconds(1);

    private static Instant Min(Instant a, Instant b) => a < b ? a : b;

    private string Describe(Iteration sprint, Instant instant)
    {
        var zone = ScheduleFor(sprint).TimeZone;
        return $"{instant.InZone(zone).ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture)} ({zone.Id})";
    }

    private bool HasLaterStarted(Iteration sprint) =>
        _sprints.Skip(IndexOf(sprint) + 1).Any(s => s.Started is not null);

    private Result CheckEligible(Iteration sprint)
    {
        if (sprint.Type != IterationType.Sprint)
            return Result.Failure("Only a sprint can be started, completed or reopened.");

        if (sprint.TeamId is null)
            return Result.Failure("The sprint's team is not mapped to a Wayd team.");

        if (!sprint.DateRange.Start.HasValue || !sprint.DateRange.End.HasValue)
            return Result.Failure("The sprint has no planned dates.");

        if (!Contains(sprint))
            throw new InvalidOperationException($"Sprint {sprint.Id} is not part of team {TeamId}'s timeline.");

        return Result.Success();
    }

    private int IndexOf(Iteration sprint)
    {
        var index = _sprints.IndexOf(sprint);
        if (index < 0)
            throw new InvalidOperationException($"Sprint {sprint.Id} is not part of team {TeamId}'s timeline.");
        return index;
    }

    private static LocalDate PlannedStartDate(Iteration sprint) =>
        sprint.DateRange.Start ?? throw new InvalidOperationException($"Sprint {sprint.Id} has no planned start.");

    private static LocalDate PlannedEndDate(Iteration sprint) =>
        sprint.DateRange.End ?? throw new InvalidOperationException($"Sprint {sprint.Id} has no planned end.");
}
