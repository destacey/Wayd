using System.Globalization;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// One team's sprints in planned order, and the rules that hold across them: when each one actually
/// starts and ends, and which of them may be started, completed or reopened.
/// </summary>
/// <remarks>
/// A sprint the team did not start or complete takes default actual dates from its schedule: it starts at
/// the end of the commitment grace period after its planned start and ends at the end of its last planned
/// day, both in the team's zone. Azure DevOps lets one team's sprints overlap and Wayd keeps the source
/// dates, so a default end is cut to the next sprint's start, which is what keeps the team's actual periods
/// from overlapping.
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

    public Instant EffectiveStart(Iteration sprint) => sprint.Started ?? DefaultStart(sprint);

    public Instant EffectiveEnd(Iteration sprint)
    {
        if (sprint.Completed is { } completed)
            return completed;

        var plannedEnd = PlannedEnd(sprint);
        var next = Next(sprint);
        if (next is null)
            return plannedEnd;

        var nextStart = EffectiveStart(next);
        return nextStart < plannedEnd ? nextStart : plannedEnd;
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
