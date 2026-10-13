using NodaTime;

namespace Wayd.Organization.Application.EmployeeCapacities;

/// <summary>An allocation as the capacity sums read it.</summary>
/// <param name="CountsTowardDeliveryCapacity">False when the allocation's role does not add to delivery capacity.</param>
/// <param name="End">The last day, or null when the allocation has no end.</param>
public sealed record CapacityAllocation(Guid EmployeeId, Guid TeamId, bool CountsTowardDeliveryCapacity, LocalDate Start, LocalDate? End, int Percent);

/// <summary>One person's time over a team's working days, in person-days.</summary>
/// <param name="AllocatedPersonDays">Their time allocated to the team, in every role.</param>
/// <param name="DeliveryPersonDays">The part of it in roles that count toward delivery capacity.</param>
/// <param name="ElsewherePersonDays">Their time allocated to other teams on the same days.</param>
public sealed record PersonCapacity(decimal AllocatedPersonDays, decimal DeliveryPersonDays, decimal ElsewherePersonDays);

/// <summary>
/// Turns allocations into person-days: an allocation of 50% over ten working days is five. Pure: everything
/// it needs is passed in.
/// </summary>
public static class TeamCapacityCalculator
{
    /// <summary>
    /// Each person's capacity for <paramref name="teamId"/> over <paramref name="workingDays"/>, by employee id.
    /// Time on other teams is counted on this team's working days too, so the three figures are comparable.
    /// </summary>
    public static IReadOnlyDictionary<Guid, PersonCapacity> Calculate(Guid teamId, IReadOnlyCollection<LocalDate> workingDays, IEnumerable<CapacityAllocation> allocations)
    {
        Dictionary<Guid, PersonCapacity> people = [];

        foreach (var person in allocations.GroupBy(a => a.EmployeeId))
        {
            decimal allocated = 0, delivery = 0, elsewhere = 0;

            foreach (var allocation in person)
            {
                var personDays = PersonDays(workingDays, allocation.Start, allocation.End, allocation.Percent);

                if (allocation.TeamId != teamId)
                {
                    elsewhere += personDays;
                    continue;
                }

                allocated += personDays;
                if (allocation.CountsTowardDeliveryCapacity)
                    delivery += personDays;
            }

            people[person.Key] = new PersonCapacity(allocated, delivery, elsewhere);
        }

        return people;
    }

    /// <summary>
    /// The person-days one allocation is worth: its percent of each of the <paramref name="workingDays"/> it covers.
    /// </summary>
    /// <param name="end">The allocation's last day, or null when it has no end.</param>
    public static decimal PersonDays(IReadOnlyCollection<LocalDate> workingDays, LocalDate start, LocalDate? end, int percent)
    {
        var days = workingDays.Count(d => start <= d && (end is null || d <= end));
        return days * percent / 100m;
    }
}
