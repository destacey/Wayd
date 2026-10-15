using NodaTime;
using Wayd.Common.Application.Requests.Organization;

namespace Wayd.Organization.Application.EmployeeCapacities;

/// <summary>
/// A team's capacity over each of several periods, counting a team of teams as its rollup does: its own
/// allocations, and those of each team beneath it on the days that team was in its hierarchy, each over its own
/// working days. Pure: everything it needs is passed in.
/// </summary>
public static class TeamPeriodCapacityCalculator
{
    /// <summary>The capacity of <paramref name="teamId"/> over each of <paramref name="periods"/>, in their order.</summary>
    /// <param name="descendants">
    /// The teams beneath it over a range holding every period, as <see cref="TeamHierarchyPeriods.Descendants"/>
    /// gives them. Empty for a team.
    /// </param>
    /// <param name="teamDays">The working days of the team and every team beneath it, by team id, over that range.</param>
    /// <param name="allocationsByTeam">The allocations to the team and every team beneath it, by team id.</param>
    public static IReadOnlyList<TeamPeriodCapacity> Calculate(
        Guid teamId,
        IReadOnlyList<CapacityPeriod> periods,
        IReadOnlyList<HierarchyNode> descendants,
        IReadOnlyDictionary<Guid, IReadOnlyList<TeamDay>> teamDays,
        ILookup<Guid, CapacityAllocation> allocationsByTeam,
        AvailabilityCalculator availability)
    {
        List<TeamPeriodCapacity> capacities = [];

        foreach (var period in periods)
        {
            HierarchyPeriod whole = new(period.Start, period.End);
            HashSet<LocalDate> daysOff = [.. period.DaysOff ?? []];
            decimal allocated = 0, delivery = 0, away = 0;

            void Count(Guid countedTeamId, IEnumerable<HierarchyPeriod> counted)
            {
                List<HierarchyPeriod> within = [.. counted
                    .Where(p => p.Start <= whole.End && whole.Start <= p.End)
                    .Select(p => new HierarchyPeriod(LocalDate.Max(p.Start, whole.Start), LocalDate.Min(p.End, whole.End)))];
                if (within.Count == 0)
                    return;

                // The days off are the team asked for's own, so they never reach the teams beneath it.
                List<TeamDay> days = teamDays.TryGetValue(countedTeamId, out var all)
                    ? [.. all.Where(d => within.Any(p => p.Includes(d.Date)) && !(countedTeamId == teamId && daysOff.Contains(d.Date)))]
                    : [];

                foreach (var allocation in allocationsByTeam[countedTeamId])
                {
                    var personDays = TeamCapacityCalculator.PersonDays(days, allocation.EmployeeId, allocation.Start, allocation.End, allocation.Percent, availability);
                    allocated += personDays;
                    away += TeamCapacityCalculator.AwayPersonDays(days, allocation.EmployeeId, allocation.Start, allocation.End, allocation.Percent, availability);
                    if (allocation.CountsTowardDeliveryCapacity)
                        delivery += personDays;
                }
            }

            void CountTree(IReadOnlyList<HierarchyNode> nodes)
            {
                foreach (var node in nodes)
                {
                    Count(node.TeamId, node.Periods);
                    CountTree(node.Children);
                }
            }

            Count(teamId, [whole]);
            CountTree(descendants);

            capacities.Add(new TeamPeriodCapacity(period, allocated, delivery, away));
        }

        return capacities;
    }
}
