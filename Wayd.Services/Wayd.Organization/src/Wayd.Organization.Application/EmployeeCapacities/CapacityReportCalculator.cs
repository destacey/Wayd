using NodaTime;

namespace Wayd.Organization.Application.EmployeeCapacities;

/// <summary>How a capacity report divides its range into columns.</summary>
public enum CapacityReportInterval
{
    /// <summary>Weeks from Monday to Sunday, the first and last cut to the range.</summary>
    Week = 1,

    /// <summary>Calendar months, the first and last cut to the range.</summary>
    Month = 2,
}

/// <summary>An allocation as the capacity report reads it.</summary>
/// <param name="RoleId">The team member role it names, or null for none.</param>
/// <param name="CountsTowardDeliveryCapacity">False when the allocation's role does not add to delivery capacity.</param>
/// <param name="End">The last day, or null when the allocation has no end.</param>
public sealed record CapacityReportAllocation(Guid EmployeeId, Guid TeamId, Guid? RoleId, bool CountsTowardDeliveryCapacity, LocalDate Start, LocalDate? End, int Percent);

/// <summary>
/// Time in person-days, from what was allocated to what was left after holidays and time away. An allocation on a
/// holiday the person takes is all lost to it; on any other day, the share they are away is lost to time away.
/// </summary>
/// <param name="Allocated">The time allocated over the working days, before holidays and time away.</param>
/// <param name="Holidays">The part of it on holidays the person takes.</param>
/// <param name="TimeAway">The part of it their time away takes.</param>
/// <param name="Available">What is left.</param>
/// <param name="Delivery">The part of <paramref name="Available"/> in roles that count toward delivery capacity.</param>
public sealed record CapacityMeasures(decimal Allocated, decimal Holidays, decimal TimeAway, decimal Available, decimal Delivery)
{
    /// <summary>No time.</summary>
    public static CapacityMeasures Zero { get; } = new(0, 0, 0, 0, 0);

    /// <summary>The two added together.</summary>
    public static CapacityMeasures operator +(CapacityMeasures a, CapacityMeasures b) =>
        new(a.Allocated + b.Allocated, a.Holidays + b.Holidays, a.TimeAway + b.TimeAway, a.Available + b.Available, a.Delivery + b.Delivery);
}

/// <summary>A team as counted in a capacity report: the root, or one place beneath it in the hierarchy.</summary>
/// <param name="NodeId">The node's position in the report, from 0 for the root, depth first.</param>
/// <param name="ParentNodeId">The node above it, or null for the root.</param>
/// <param name="Periods">The days of the range it counts for. The whole range for the root.</param>
/// <param name="WorkingDays">Its own working days within <paramref name="Periods"/> that are not team holidays.</param>
public sealed record CapacityReportNode(int NodeId, int? ParentNodeId, Guid TeamId, IReadOnlyList<HierarchyPeriod> Periods, int WorkingDays);

/// <summary>One person's time in one role on one node of a capacity report.</summary>
/// <param name="Buckets">The time in each of the report's buckets, in their order; empty when the report has none.</param>
public sealed record CapacityReportLine(int NodeId, Guid EmployeeId, Guid? RoleId, bool CountsTowardDeliveryCapacity, CapacityMeasures Total, IReadOnlyList<CapacityMeasures> Buckets);

/// <summary>
/// A person's time outside the report, over the system default working days: the time allocated anywhere the
/// report does not count, and the time allocated nowhere.
/// </summary>
/// <param name="Outside">Their time allocated to teams outside the hierarchy, or to a team in it on days it was not there.</param>
/// <param name="Unallocated">Their time not allocated to any team.</param>
/// <param name="OutsideBuckets">The outside time in each bucket.</param>
/// <param name="UnallocatedBuckets">The unallocated time in each bucket.</param>
public sealed record CapacityReportPerson(Guid EmployeeId, decimal Outside, decimal Unallocated, IReadOnlyList<decimal> OutsideBuckets, IReadOnlyList<decimal> UnallocatedBuckets);

/// <summary>
/// The days a person was employed, which bound their unallocated time: a day before they joined or after they
/// left is not time they had to give.
/// </summary>
/// <param name="First">Their first day, or null when it is not known.</param>
/// <param name="Last">Their last day, or null while they are still employed.</param>
public sealed record EmploymentPeriod(LocalDate? First, LocalDate? Last)
{
    /// <summary>Whether <paramref name="day"/> falls within it.</summary>
    public bool Includes(LocalDate day) => (First is null || First <= day) && (Last is null || day <= Last);
}

/// <summary>A capacity report before names are put to its ids.</summary>
public sealed record CapacityReport(
    IReadOnlyList<HierarchyPeriod> Buckets,
    IReadOnlyList<CapacityReportNode> Nodes,
    IReadOnlyList<CapacityReportLine> Lines,
    IReadOnlyList<CapacityReportPerson> People);

/// <summary>
/// Works out a capacity report for a team or a team of teams: each allocation counted on the team it was made
/// to, over that team's working days on the days it sat beneath the root, so a person allocated at two levels has
/// time at each and none of it twice. Pure: everything it needs is passed in.
/// </summary>
public static class CapacityReportCalculator
{
    private const decimal Whole = 100m;

    /// <summary>The report for <paramref name="rootId"/> from <paramref name="from"/> to <paramref name="to"/>, both inclusive.</summary>
    /// <param name="descendants">The teams beneath it, as <see cref="TeamHierarchyPeriods.Descendants"/> gives them. Empty for a team.</param>
    /// <param name="teamDays">The working days of the root and every team beneath it over the range, by team id.</param>
    /// <param name="allocations">
    /// Every allocation in the range of everyone allocated to the root or a team beneath it, to any team: those
    /// to teams the report does not count make up their time outside it.
    /// </param>
    /// <param name="defaultDays">The system default working days over the range, which time outside the report is counted over.</param>
    /// <param name="interval">How to bucket the range, or null for totals only.</param>
    /// <param name="employment">
    /// The days each person was employed, by employee id; a person left out was employed throughout.
    /// </param>
    public static CapacityReport Calculate(
        Guid rootId,
        LocalDate from,
        LocalDate to,
        IReadOnlyList<HierarchyNode> descendants,
        IReadOnlyDictionary<Guid, IReadOnlyList<TeamDay>> teamDays,
        IReadOnlyCollection<CapacityReportAllocation> allocations,
        IReadOnlyList<TeamDay> defaultDays,
        AvailabilityCalculator availability,
        CapacityReportInterval? interval,
        IReadOnlyDictionary<Guid, EmploymentPeriod>? employment = null)
    {
        var buckets = Buckets(from, to, interval);
        var bucketOf = BucketIndex(buckets);
        var allocationsByTeam = allocations.ToLookup(a => a.TeamId);

        List<CapacityReportNode> nodes = [];
        List<CapacityReportLine> lines = [];

        void Count(Guid teamId, int? parentNodeId, IReadOnlyList<HierarchyPeriod> periods)
        {
            var nodeId = nodes.Count;
            List<TeamDay> days = [.. (teamDays.GetValueOrDefault(teamId) ?? []).Where(d => periods.Any(p => p.Includes(d.Date)))];
            nodes.Add(new CapacityReportNode(nodeId, parentNodeId, teamId, periods, days.Count(d => !availability.IsTeamHoliday(d))));

            foreach (var line in allocationsByTeam[teamId]
                .Where(a => periods.Any(p => p.Overlaps(a.Start, a.End)))
                .GroupBy(a => (a.EmployeeId, a.RoleId)))
            {
                var total = CapacityMeasures.Zero;
                var byBucket = Enumerable.Repeat(CapacityMeasures.Zero, buckets.Count).ToArray();
                var counts = line.First().CountsTowardDeliveryCapacity;

                foreach (var allocation in line)
                {
                    foreach (var day in days.Where(d => allocation.Start <= d.Date && (allocation.End is null || d.Date <= allocation.End)))
                    {
                        var measures = DayMeasures(allocation, day, availability);
                        total += measures;
                        if (bucketOf.TryGetValue(day.Date, out var index))
                            byBucket[index] += measures;
                    }
                }

                lines.Add(new CapacityReportLine(nodeId, line.Key.EmployeeId, line.Key.RoleId, counts, total, byBucket));
            }
        }

        void CountTree(IReadOnlyList<HierarchyNode> children, int parentNodeId)
        {
            foreach (var child in children)
            {
                var nodeId = nodes.Count;
                Count(child.TeamId, parentNodeId, child.Periods);
                CountTree(child.Children, nodeId);
            }
        }

        Count(rootId, null, [new HierarchyPeriod(from, to)]);
        CountTree(descendants, 0);

        return new CapacityReport(buckets, nodes, lines, People(nodes, lines, allocations, defaultDays, buckets, bucketOf, availability, employment));
    }

    /// <summary>The buckets <paramref name="interval"/> divides the range into, the first and last cut to it.</summary>
    public static IReadOnlyList<HierarchyPeriod> Buckets(LocalDate from, LocalDate to, CapacityReportInterval? interval)
    {
        if (interval is null)
            return [];

        List<HierarchyPeriod> buckets = [];
        var start = from;
        while (start <= to)
        {
            var end = interval switch
            {
                CapacityReportInterval.Week => start.With(DateAdjusters.NextOrSame(IsoDayOfWeek.Sunday)),
                _ => start.With(DateAdjusters.EndOfMonth),
            };
            end = LocalDate.Min(end, to);
            buckets.Add(new HierarchyPeriod(start, end));
            start = end.PlusDays(1);
        }

        return buckets;
    }

    /// <summary>
    /// Each person's time outside the report on the system default working days: what they have allocated that
    /// the report did not count, and what is allocated nowhere on the days they were employed. Neither is reduced
    /// by holidays or time away.
    /// </summary>
    private static List<CapacityReportPerson> People(
        IReadOnlyList<CapacityReportNode> nodes,
        IReadOnlyList<CapacityReportLine> lines,
        IReadOnlyCollection<CapacityReportAllocation> allocations,
        IReadOnlyList<TeamDay> defaultDays,
        IReadOnlyList<HierarchyPeriod> buckets,
        IReadOnlyDictionary<LocalDate, int> bucketOf,
        AvailabilityCalculator availability,
        IReadOnlyDictionary<Guid, EmploymentPeriod>? employment)
    {
        var countedPeriods = nodes.ToLookup(n => n.TeamId, n => n.Periods);
        var allocationsByEmployee = allocations.ToLookup(a => a.EmployeeId);

        List<CapacityReportPerson> people = [];
        foreach (var employeeId in lines.Select(l => l.EmployeeId).Distinct())
        {
            decimal outside = 0, unallocated = 0;
            var outsideByBucket = new decimal[buckets.Count];
            var unallocatedByBucket = new decimal[buckets.Count];
            var employed = employment?.GetValueOrDefault(employeeId);

            foreach (var day in defaultDays)
            {
                int allocated = 0, counted = 0;
                foreach (var allocation in allocationsByEmployee[employeeId])
                {
                    if (allocation.Start > day.Date || allocation.End < day.Date)
                        continue;

                    allocated += allocation.Percent;
                    if (countedPeriods[allocation.TeamId].Any(periods => periods.Any(p => p.Includes(day.Date))))
                        counted += allocation.Percent;
                }

                var dayOutside = availability.PersonDays(employeeId, (allocated - counted) / Whole);
                var dayUnallocated = employed is null || employed.Includes(day.Date)
                    ? availability.PersonDays(employeeId, Math.Max(0, Whole - allocated) / Whole)
                    : 0;
                outside += dayOutside;
                unallocated += dayUnallocated;
                if (bucketOf.TryGetValue(day.Date, out var index))
                {
                    outsideByBucket[index] += dayOutside;
                    unallocatedByBucket[index] += dayUnallocated;
                }
            }

            people.Add(new CapacityReportPerson(employeeId, outside, unallocated, outsideByBucket, unallocatedByBucket));
        }

        return people;
    }

    /// <summary>What one allocation is worth on one of its team's working days, measured as the other capacity views measure it.</summary>
    private static CapacityMeasures DayMeasures(CapacityReportAllocation allocation, TeamDay day, AvailabilityCalculator availability)
    {
        var share = allocation.Percent / Whole;
        var allocated = availability.PersonDays(allocation.EmployeeId, share * day.ShareWorked);

        if (availability.IsHoliday(allocation.EmployeeId, day))
            return new CapacityMeasures(allocated, allocated, 0, 0, 0);

        var away = availability.PersonDays(allocation.EmployeeId, availability.ShareAway(allocation.EmployeeId, day.Date) * day.ShareWorked * share);
        var available = availability.PersonDays(allocation.EmployeeId, share * availability.Available(allocation.EmployeeId, day));
        return new CapacityMeasures(allocated, 0, away, available, allocation.CountsTowardDeliveryCapacity ? available : 0);
    }

    /// <summary>The position of the bucket each day of the range falls in, by date. Empty when there are no buckets.</summary>
    private static Dictionary<LocalDate, int> BucketIndex(IReadOnlyList<HierarchyPeriod> buckets)
    {
        Dictionary<LocalDate, int> index = [];
        for (var i = 0; i < buckets.Count; i++)
        {
            for (var day = buckets[i].Start; day <= buckets[i].End; day = day.PlusDays(1))
                index[day] = i;
        }

        return index;
    }
}
