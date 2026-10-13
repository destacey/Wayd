using NodaTime;
using Wayd.Common.Domain.Events.Organization;

namespace Wayd.Organization.Domain.Models;

/// <summary>
/// The date arithmetic behind an employee's allocations, on values alone so a change can be worked out and
/// checked in full before any of it is applied. A slot is a team and role; within a slot, allocations never
/// overlap. An allocation with no end runs forever.
/// </summary>
internal static class AllocationTimeline
{
    /// <summary>
    /// Replaces what the change's slot holds over the change's range, splitting any allocation that runs past
    /// either end of it.
    /// </summary>
    public static List<EmployeeAllocationDetails> Set(IEnumerable<EmployeeAllocationDetails> allocations, AllocationChange change)
    {
        List<EmployeeAllocationDetails> result = [];

        foreach (var allocation in allocations)
        {
            if (!InSlot(allocation, change.TeamId, change.RoleId) || !Overlaps(allocation, change.Start, change.End))
            {
                result.Add(allocation);
                continue;
            }

            if (allocation.Start < change.Start)
                result.Add(allocation with { End = change.Start.PlusDays(-1) });

            if (change.End is { } end && (allocation.End is null || allocation.End > end))
                result.Add(allocation with { Start = end.PlusDays(1) });
        }

        if (change.Percent > 0)
            result.Add(new EmployeeAllocationDetails(change.TeamId, change.RoleId, change.Start, change.End, change.Percent));

        return result;
    }

    /// <summary>
    /// What a slot holds on each stretch of a range, in date order and covering the whole range: a stretch
    /// with no allocation is returned at zero percent.
    /// </summary>
    public static List<EmployeeAllocationDetails> Slices(IEnumerable<EmployeeAllocationDetails> allocations, Guid teamId, Guid? roleId, LocalDate start, LocalDate? end)
    {
        List<EmployeeAllocationDetails> slices = [];

        // Null once a slice with no end has been taken, when nothing of the range is left.
        LocalDate? uncovered = start;

        foreach (var allocation in allocations.Where(a => InSlot(a, teamId, roleId) && Overlaps(a, start, end)).OrderBy(a => a.Start))
        {
            var sliceStart = LocalDate.Max(allocation.Start, start);
            if (uncovered < sliceStart)
                slices.Add(new EmployeeAllocationDetails(teamId, roleId, uncovered.Value, sliceStart.PlusDays(-1), 0));

            var sliceEnd = EarlierEnd(allocation.End, end);
            slices.Add(new EmployeeAllocationDetails(teamId, roleId, sliceStart, sliceEnd, allocation.Percent));

            uncovered = sliceEnd?.PlusDays(1);
            if (uncovered is null)
                break;
        }

        if (uncovered is { } rest && (end is null || rest <= end))
            slices.Add(new EmployeeAllocationDetails(teamId, roleId, rest, end, 0));

        return slices;
    }

    /// <summary>Joins allocations in the same slot that run on from one another at the same percent.</summary>
    public static List<EmployeeAllocationDetails> Normalize(IEnumerable<EmployeeAllocationDetails> allocations)
    {
        List<EmployeeAllocationDetails> result = [];

        foreach (var slot in allocations.GroupBy(a => (a.TeamId, a.RoleId)))
        {
            EmployeeAllocationDetails? open = null;

            foreach (var allocation in slot.OrderBy(a => a.Start))
            {
                if (open is not null && open.Percent == allocation.Percent && open.End?.PlusDays(1) == allocation.Start)
                {
                    open = open with { End = allocation.End };
                    continue;
                }

                if (open is not null)
                    result.Add(open);

                open = allocation;
            }

            if (open is not null)
                result.Add(open);
        }

        return [.. result.OrderBy(a => a.Start).ThenBy(a => a.TeamId).ThenBy(a => a.RoleId)];
    }

    /// <summary>
    /// The first day the allocations add up to more than <paramref name="limit"/> percent, with the total that
    /// day, or null when no day does.
    /// </summary>
    public static (LocalDate Day, int Percent)? FirstDayOver(IReadOnlyCollection<EmployeeAllocationDetails> allocations, int limit)
    {
        // The total only rises on a day an allocation starts, so those are the only days that can breach.
        foreach (var day in allocations.Select(a => a.Start).Distinct().Order())
        {
            var total = allocations.Where(a => a.Start <= day && (a.End is null || day <= a.End)).Sum(a => a.Percent);
            if (total > limit)
                return (day, total);
        }

        return null;
    }

    private static bool InSlot(EmployeeAllocationDetails allocation, Guid teamId, Guid? roleId) =>
        allocation.TeamId == teamId && allocation.RoleId == roleId;

    private static bool Overlaps(EmployeeAllocationDetails allocation, LocalDate start, LocalDate? end) =>
        (end is null || allocation.Start <= end) && (allocation.End is null || start <= allocation.End);

    private static LocalDate? EarlierEnd(LocalDate? first, LocalDate? second)
    {
        if (first is null)
            return second;

        return second is null ? first : LocalDate.Min(first.Value, second.Value);
    }
}
