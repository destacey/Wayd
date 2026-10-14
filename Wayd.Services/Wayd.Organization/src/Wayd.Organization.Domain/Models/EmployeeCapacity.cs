using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;

namespace Wayd.Organization.Domain.Models;

/// <summary>
/// How an employee's time is shared between teams over time, and when they are not there to give it: the
/// holidays they take and the days they are away. One per employee, because the rule it keeps spans teams:
/// on no day may an employee's allocations add up to more than 100%.
/// </summary>
/// <remarks>
/// Its <see cref="BaseEntity{TId}.Id"/> is the employee's id. Allocation is separate from team membership: a
/// team can have the time of someone who is not a member, and a member can have none allocated. Time away is
/// separate from allocation: it takes nothing from the 100%.
/// </remarks>
public sealed class EmployeeCapacity : BaseAuditableEntity
{
    /// <summary>The most of an employee's time that can be allocated on any day, as a percent.</summary>
    public const int FullAllocation = 100;

    private readonly List<EmployeeTeamAllocation> _allocations = [];
    private readonly List<EmployeeTimeAway> _timeAway = [];

    private EmployeeCapacity() { }

    private EmployeeCapacity(Guid employeeId)
    {
        Id = Guard.Against.Default(employeeId);
    }

    /// <summary>
    /// Counts the changes made to the record. It is the concurrency token: two changes worked out from the
    /// same allocations could each fit within 100% and together exceed it, as could two stretches of time away
    /// within a day, so the second to save must fail.
    /// </summary>
    public int Revision { get; private set; }

    /// <summary>
    /// The employee's allocations. Within one team and role they never overlap, and ones that run on from one
    /// another at the same percent are held as one.
    /// </summary>
    public IReadOnlyCollection<EmployeeTeamAllocation> Allocations => _allocations.AsReadOnly();

    /// <summary>
    /// The holiday calendar the employee follows on every team, such as their country's, or null when they
    /// follow the system default. A shared calendar, not one of their own: many employees follow the same one.
    /// </summary>
    public Guid? HolidayCalendarId { get; private set; }

    /// <summary>
    /// The days the employee is away from every team. Stretches may overlap, such as training in the morning
    /// and leave in the afternoon, but never add up to more than a whole day.
    /// </summary>
    public IReadOnlyCollection<EmployeeTimeAway> TimeAway => _timeAway.AsReadOnly();

    /// <summary>
    /// Sets the share of the employee's time each change names, all together or not at all. Fails when a day
    /// would then be allocated beyond 100%, leaving the allocations as they were.
    /// </summary>
    public Result ChangeAllocations(IReadOnlyCollection<AllocationChange> changes, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(changes);

        var target = Current();

        foreach (var change in changes)
        {
            if (change.TeamId == Guid.Empty)
                return Result.Failure("An allocation needs a team.");

            if (change.Percent is < 0 or > FullAllocation)
                return Result.Failure($"An allocation must be between 0% and {FullAllocation}%.");

            var range = ValidateRange(change.Start, change.End);
            if (range.IsFailure)
                return range;

            target = AllocationTimeline.Set(target, change);
        }

        return Apply(target, actor, timestamp);
    }

    /// <summary>
    /// Moves a share of the employee's time from one team and role to another over a date range, as one
    /// change, so a move between teams never has to pass through a day over or under what was intended. Fails
    /// when the source has less than <paramref name="percent"/> on any day of the range.
    /// </summary>
    public Result MoveAllocation(Guid fromTeamId, Guid? fromRoleId, Guid toTeamId, Guid? toRoleId, LocalDate start, LocalDate? end, int percent, EventActor actor, Instant timestamp)
    {
        if (fromTeamId == Guid.Empty || toTeamId == Guid.Empty)
            return Result.Failure("An allocation needs a team.");

        if (fromTeamId == toTeamId && fromRoleId == toRoleId)
            return Result.Failure("An allocation cannot be moved to the team and role it is already in.");

        if (percent is < 1 or > FullAllocation)
            return Result.Failure($"The share to move must be between 1% and {FullAllocation}%.");

        var range = ValidateRange(start, end);
        if (range.IsFailure)
            return range;

        var target = Current();

        foreach (var slice in AllocationTimeline.Slices(target, fromTeamId, fromRoleId, start, end))
        {
            if (slice.Percent < percent)
                return Result.Failure($"Only {slice.Percent}% is allocated to move from {slice.Start:yyyy-MM-dd}.");

            target = AllocationTimeline.Set(target, new AllocationChange(fromTeamId, fromRoleId, slice.Start, slice.End, slice.Percent - percent));
        }

        foreach (var slice in AllocationTimeline.Slices(target, toTeamId, toRoleId, start, end))
        {
            target = AllocationTimeline.Set(target, new AllocationChange(toTeamId, toRoleId, slice.Start, slice.End, slice.Percent + percent));
        }

        return Apply(target, actor, timestamp);
    }

    /// <summary>
    /// Ends the employee's time on every team after <paramref name="lastDay"/>: an allocation running past it
    /// ends that day, and one starting later is removed. What came before is kept.
    /// </summary>
    public Result EndAllocations(LocalDate lastDay, EventActor actor, Instant timestamp) =>
        EndAfter(lastDay, _ => true, actor, timestamp);

    /// <summary>
    /// Ends the employee's time on one team, in every role, after <paramref name="lastDay"/>: an allocation
    /// running past it ends that day, and one starting later is removed. What came before, and the time on
    /// every other team, is kept.
    /// </summary>
    public Result EndAllocationsTo(Guid teamId, LocalDate lastDay, EventActor actor, Instant timestamp)
    {
        if (teamId == Guid.Empty)
            return Result.Failure("An allocation needs a team.");

        return EndAfter(lastDay, a => a.TeamId == teamId, actor, timestamp);
    }

    /// <summary>
    /// Sets the holiday calendar the employee follows, or with null has them follow the system default. Checking that the calendar exists is the caller's.
    /// </summary>
    public Result ChangeHolidayCalendar(Guid? holidayCalendarId, EventActor actor, Instant timestamp)
    {
        if (holidayCalendarId == Guid.Empty)
            return Result.Failure("A holiday calendar needs an id.");

        if (HolidayCalendarId == holidayCalendarId)
            return Result.Success();

        var previous = HolidayCalendarId;
        HolidayCalendarId = holidayCalendarId;
        Revision++;

        AddDomainEvent(new EmployeeHolidayCalendarChangedEvent(Id, previous, HolidayCalendarId, actor, timestamp));
        return Result.Success();
    }

    /// <summary>
    /// Records the employee as away from every team from <paramref name="start"/> to <paramref name="end"/>,
    /// for <paramref name="percentAway"/> of each day. Fails when a day would then be more than a whole day away.
    /// </summary>
    public Result<EmployeeTimeAway> RecordTimeAway(LocalDate start, LocalDate end, int percentAway, TimeAwayReason reason, EventActor actor, Instant timestamp)
    {
        var valid = ValidateTimeAway(null, start, end, percentAway, reason);
        if (valid.IsFailure)
            return Result.Failure<EmployeeTimeAway>(valid.Error);

        var timeAway = new EmployeeTimeAway(Id, start, end, percentAway, reason);
        _timeAway.Add(timeAway);
        Revision++;

        AddDomainEvent(new EmployeeTimeAwayRecordedEvent(Id, timeAway.Id, start, end, percentAway, actor, timestamp));
        return timeAway;
    }

    /// <summary>
    /// Changes the days of a stretch of time away, how much of each day it takes, or its reason. Fails when a
    /// day would then be more than a whole day away, leaving the time away as it was.
    /// </summary>
    public Result ChangeTimeAway(Guid timeAwayId, LocalDate start, LocalDate end, int percentAway, TimeAwayReason reason, EventActor actor, Instant timestamp)
    {
        var timeAway = _timeAway.Find(t => t.Id == timeAwayId);
        if (timeAway is null)
            return Result.Failure($"Time away {timeAwayId} is not this employee's.");

        var valid = ValidateTimeAway(timeAwayId, start, end, percentAway, reason);
        if (valid.IsFailure)
            return valid;

        var previousStart = timeAway.Start;
        var previousEnd = timeAway.End;
        var previousPercentAway = timeAway.PercentAway;
        var reasonChanged = timeAway.Reason != reason;

        if (previousStart == start && previousEnd == end && previousPercentAway == percentAway && !reasonChanged)
            return Result.Success();

        timeAway.Change(start, end, percentAway, reason);
        Revision++;

        AddDomainEvent(new EmployeeTimeAwayChangedEvent(Id, timeAway.Id, start, end, percentAway, previousStart, previousEnd, previousPercentAway, reasonChanged, actor, timestamp));
        return Result.Success();
    }

    /// <summary>Removes a stretch of time away, so the employee counts as available on those days again.</summary>
    public Result RemoveTimeAway(Guid timeAwayId, EventActor actor, Instant timestamp)
    {
        var timeAway = _timeAway.Find(t => t.Id == timeAwayId);
        if (timeAway is null)
            return Result.Failure($"Time away {timeAwayId} is not this employee's.");

        _timeAway.Remove(timeAway);
        Revision++;

        AddDomainEvent(new EmployeeTimeAwayRemovedEvent(Id, timeAway.Id, timeAway.Start, timeAway.End, timeAway.PercentAway, actor, timestamp));
        return Result.Success();
    }

    /// <summary>Creates the capacity record for an employee, with nothing allocated.</summary>
    public static EmployeeCapacity Create(Guid employeeId) => new(employeeId);

    private List<EmployeeAllocationDetails> Current() => [.. _allocations.Select(a => a.ToDetails())];

    private Result EndAfter(LocalDate lastDay, Func<EmployeeAllocationDetails, bool> ending, EventActor actor, Instant timestamp)
    {
        var target = Current();
        var firstDayOff = lastDay.PlusDays(1);

        foreach (var (teamId, roleId) in target.Where(ending).Select(a => (a.TeamId, a.RoleId)).Distinct().ToList())
        {
            target = AllocationTimeline.Set(target, new AllocationChange(teamId, roleId, firstDayOff, null, 0));
        }

        return Apply(target, actor, timestamp);
    }

    /// <summary>
    /// Whether a stretch of time away is well formed and leaves no day more than a whole day away.
    /// </summary>
    /// <param name="timeAwayId">The stretch being changed, which is left out of the day totals; null for a new one.</param>
    private Result ValidateTimeAway(Guid? timeAwayId, LocalDate start, LocalDate end, int percentAway, TimeAwayReason reason)
    {
        if (end < start)
            return Result.Failure("Time away cannot end before it starts.");

        if (percentAway is < 1 or > EmployeeTimeAway.FullDay)
            return Result.Failure($"Time away must be between 1% and {EmployeeTimeAway.FullDay}% of a day.");

        if (!Enum.IsDefined(reason))
            return Result.Failure("Time away needs a reason.");

        var others = _timeAway.Where(t => t.Id != timeAwayId && t.Start <= end && t.End >= start).ToList();

        // The total within the stretch only rises on its first day or on a day another stretch starts.
        foreach (var day in others.Select(t => t.Start).Where(d => d > start).Prepend(start).Distinct().Order())
        {
            var total = percentAway + others.Where(t => t.Covers(day)).Sum(t => t.PercentAway);
            if (total > EmployeeTimeAway.FullDay)
                return Result.Failure($"The employee would be away for {total}% of {day:yyyy-MM-dd}. Time away cannot add up to more than a whole day.");
        }

        return Result.Success();
    }

    private static Result ValidateRange(LocalDate start, LocalDate? end) =>
        end < start
            ? Result.Failure("An allocation cannot end before it starts.")
            : Result.Success();

    private Result Apply(List<EmployeeAllocationDetails> target, EventActor actor, Instant timestamp)
    {
        target = AllocationTimeline.Normalize(target);

        if (AllocationTimeline.FirstDayOver(target, FullAllocation) is { } over)
            return Result.Failure($"The employee would be {over.Percent}% allocated on {over.Day:yyyy-MM-dd}. An employee cannot be allocated more than {FullAllocation}% across all teams.");

        var before = AllocationTimeline.Normalize(Current());
        List<EmployeeAllocationDetails> added = [.. target.Except(before)];
        List<EmployeeAllocationDetails> removed = [.. before.Except(target)];

        if (added.Count == 0 && removed.Count == 0)
            return Result.Success();

        var leaving = _allocations.Where(a => removed.Contains(a.ToDetails())).ToList();

        foreach (var details in added)
        {
            // An allocation that keeps its team, role and start is the same one resized, so its row carries on.
            var resized = leaving.Find(a => a.TeamId == details.TeamId && a.RoleId == details.RoleId && a.Start == details.Start);
            if (resized is null)
            {
                _allocations.Add(new EmployeeTeamAllocation(Id, details));
                continue;
            }

            resized.Resize(details.End, details.Percent);
            leaving.Remove(resized);
        }

        foreach (var allocation in leaving)
            _allocations.Remove(allocation);

        Revision++;

        AddDomainEvent(new EmployeeAllocationsChangedEvent(Id, added, removed, target, actor, timestamp));
        return Result.Success();
    }
}
