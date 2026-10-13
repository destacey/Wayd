using NodaTime;

namespace Wayd.Organization.Application.EmployeeCapacities;

/// <summary>A child's membership of a team of teams, as the hierarchy walk reads it.</summary>
/// <param name="End">The last day, or null when the membership has no end.</param>
public sealed record HierarchyMembership(Guid ChildId, Guid ParentId, LocalDate Start, LocalDate? End);

/// <summary>A run of days, both inclusive.</summary>
public sealed record HierarchyPeriod(LocalDate Start, LocalDate End)
{
    public bool Includes(LocalDate day) => Start <= day && day <= End;

    /// <summary>Whether a range from <paramref name="start"/> to <paramref name="end"/>, or with no end, shares a day with this one.</summary>
    public bool Overlaps(LocalDate start, LocalDate? end) => start <= End && (end is null || end >= Start);
}

/// <summary>A team beneath a team of teams, with the days it counts there.</summary>
/// <param name="Periods">
/// The days the team was beneath the root through this path, in date order and never touching: the days its
/// membership of its parent covers, less any its parent was itself out of the hierarchy.
/// </param>
/// <param name="Children">The teams beneath this one. Empty for a team.</param>
public sealed record HierarchyNode(Guid TeamId, IReadOnlyList<HierarchyPeriod> Periods, IReadOnlyList<HierarchyNode> Children);

/// <summary>
/// Works out which teams were beneath a team of teams over a date range, and on which days. Pure: everything
/// it needs is passed in.
/// </summary>
public static class TeamHierarchyPeriods
{
    /// <summary>
    /// The teams beneath <paramref name="rootId"/> from <paramref name="from"/> to <paramref name="to"/>, both
    /// inclusive, as a tree. A team with no day beneath the root in the range is left out, with everything
    /// under it. A team that moved between two parents in the range appears under each, for its days there.
    /// </summary>
    public static IReadOnlyList<HierarchyNode> Descendants(Guid rootId, LocalDate from, LocalDate to, IEnumerable<HierarchyMembership> memberships)
    {
        var byParent = memberships.ToLookup(m => m.ParentId);

        return Children(rootId, [new HierarchyPeriod(from, to)], [rootId]);

        List<HierarchyNode> Children(Guid parentId, IReadOnlyList<HierarchyPeriod> parentPeriods, HashSet<Guid> path)
        {
            List<HierarchyNode> children = [];

            foreach (var child in byParent[parentId].GroupBy(m => m.ChildId))
            {
                // Memberships are dated, so a loop can form across time though never on one day. A team
                // already on the path is not walked again.
                if (path.Contains(child.Key))
                    continue;

                var periods = Merge(
                    from membership in child
                    from parentPeriod in parentPeriods
                    where parentPeriod.Overlaps(membership.Start, membership.End)
                    select new HierarchyPeriod(
                        LocalDate.Max(membership.Start, parentPeriod.Start),
                        membership.End is { } end ? LocalDate.Min(end, parentPeriod.End) : parentPeriod.End));

                if (periods.Count == 0)
                    continue;

                children.Add(new HierarchyNode(child.Key, periods, Children(child.Key, periods, [.. path, child.Key])));
            }

            return children;
        }
    }

    /// <summary>The periods in date order, with any that overlap or run on from one another joined.</summary>
    private static List<HierarchyPeriod> Merge(IEnumerable<HierarchyPeriod> periods)
    {
        List<HierarchyPeriod> merged = [];

        foreach (var period in periods.OrderBy(p => p.Start))
        {
            if (merged.Count > 0 && period.Start <= merged[^1].End.PlusDays(1))
                merged[^1] = merged[^1] with { End = LocalDate.Max(merged[^1].End, period.End) };
            else
                merged.Add(period);
        }

        return merged;
    }
}
