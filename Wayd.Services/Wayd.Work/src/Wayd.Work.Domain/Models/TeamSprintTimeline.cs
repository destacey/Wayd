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

    public Result CanStart(Iteration sprint, Instant now)
    {
        var eligible = CheckEligible(sprint);
        if (eligible.IsFailure)
            return eligible;

        if (sprint.Started is not null)
            return Result.Failure("The sprint has already been started.");

        if (HasLaterStarted(sprint))
            return Result.Failure("A later sprint for this team has already been started.");

        if (now >= PlannedEnd(sprint))
            return Result.Failure("The sprint's planned end has passed.");

        var previous = Previous(sprint);
        if (previous is null)
        {
            var schedule = ScheduleFor(sprint);
            var earliest = PlannedStartDate(sprint).PlusDays(-EarlyStartDays)
                .AtStartOfDayInZone(schedule.TimeZone)
                .ToInstant();
            if (now < earliest)
                return Result.Failure($"The team's first sprint can be started at most {EarlyStartDays} days before its planned start.");
        }
        else if (now <= EffectiveStart(previous))
        {
            return Result.Failure("The sprint can't be started until the previous sprint has started.");
        }

        // The sprint in effect may be started late, or the one after it early; anything further ahead
        // would skip the sprint between them.
        var current = Current(now);
        if (current is not null && current != sprint && Next(current) != sprint)
            return Result.Failure("Only the team's next sprint can be started.");

        return Result.Success();
    }

    public Result CanComplete(Iteration sprint, Instant now)
    {
        var eligible = CheckEligible(sprint);
        if (eligible.IsFailure)
            return eligible;

        if (sprint.Completed is not null)
            return Result.Failure("The sprint has already been completed.");

        if (HasLaterStarted(sprint))
            return Result.Failure("A later sprint for this team has already been started, which ended this one.");

        if (_sprints.Take(IndexOf(sprint)).Any(s => s.Started is not null && s.Completed is null))
            return Result.Failure("An earlier sprint for this team is still open. Complete it first.");

        if (now <= EffectiveStart(sprint))
            return Result.Failure("The sprint can't be completed before it starts.");

        return Result.Success();
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

    /// <summary>The latest sprint whose actual or default start has passed.</summary>
    private Iteration? Current(Instant now)
    {
        for (var i = _sprints.Count - 1; i >= 0; i--)
        {
            if (EffectiveStart(_sprints[i]) <= now)
                return _sprints[i];
        }
        return null;
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
