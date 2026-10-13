using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;

namespace Wayd.Organization.Domain.Models;

/// <summary>
/// How an employee's time is shared between teams over time. One per employee, because the rule it keeps
/// spans teams: on no day may an employee's allocations add up to more than 100%.
/// </summary>
/// <remarks>
/// Its <see cref="BaseEntity{TId}.Id"/> is the employee's id. Allocation is separate from team membership: a
/// team can have the time of someone who is not a member, and a member can have none allocated.
/// </remarks>
public sealed class EmployeeCapacity : BaseAuditableEntity
{
    /// <summary>The most of an employee's time that can be allocated on any day, as a percent.</summary>
    public const int FullAllocation = 100;

    private readonly List<EmployeeTeamAllocation> _allocations = [];

    private EmployeeCapacity() { }

    private EmployeeCapacity(Guid employeeId)
    {
        Id = Guard.Against.Default(employeeId);
    }

    /// <summary>
    /// Counts the changes made to the allocations. It is the concurrency token: two changes worked out from the
    /// same allocations could each fit within 100% and together exceed it, so the second to save must fail.
    /// </summary>
    public int Revision { get; private set; }

    /// <summary>
    /// The employee's allocations. Within one team and role they never overlap, and ones that run on from one
    /// another at the same percent are held as one.
    /// </summary>
    public IReadOnlyCollection<EmployeeTeamAllocation> Allocations => _allocations.AsReadOnly();

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
    public Result EndAllocations(LocalDate lastDay, EventActor actor, Instant timestamp)
    {
        var target = Current();
        var firstDayOff = lastDay.PlusDays(1);

        foreach (var (teamId, roleId) in target.Select(a => (a.TeamId, a.RoleId)).Distinct().ToList())
        {
            target = AllocationTimeline.Set(target, new AllocationChange(teamId, roleId, firstDayOff, null, 0));
        }

        return Apply(target, actor, timestamp);
    }

    /// <summary>Creates the capacity record for an employee, with nothing allocated.</summary>
    public static EmployeeCapacity Create(Guid employeeId) => new(employeeId);

    private List<EmployeeAllocationDetails> Current() => [.. _allocations.Select(a => a.ToDetails())];

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
