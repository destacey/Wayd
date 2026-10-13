using NodaTime;

namespace Wayd.Organization.Application.EmployeeCapacities;

/// <summary>
/// The sums behind proposing a team's allocations from its members. Pure: everything it needs is passed in.
/// </summary>
public static class AllocationProposal
{
    /// <summary>The most of an employee's time that can be allocated on any day, as a percent.</summary>
    private const int FullAllocation = 100;

    /// <summary>
    /// The share of a person's time that an allocation from <paramref name="from"/> to <paramref name="to"/>
    /// could take: what is left of 100% on their most allocated day in that stretch.
    /// </summary>
    /// <param name="allocations">The person's allocations across all teams, as start, end and percent.</param>
    /// <param name="to">The last day, or null for every day from <paramref name="from"/> on.</param>
    public static int RemainingPercent(IReadOnlyCollection<(LocalDate Start, LocalDate? End, int Percent)> allocations, LocalDate from, LocalDate? to = null)
    {
        // The total can only rise on a day an allocation starts.
        var days = allocations.Where(a => a.Start > from && (to is null || a.Start <= to)).Select(a => a.Start).Append(from);

        var most = days.Max(day => allocations
            .Where(a => a.Start <= day && (a.End is null || day <= a.End))
            .Sum(a => a.Percent));

        return Math.Max(FullAllocation - most, 0);
    }

    /// <summary>
    /// Splits <paramref name="percent"/> into <paramref name="parts"/> whole shares that add up to it, as evenly
    /// as whole numbers allow: the first shares take the odd points.
    /// </summary>
    public static IReadOnlyList<int> SplitEvenly(int percent, int parts)
    {
        var share = percent / parts;
        var odd = percent % parts;

        return [.. Enumerable.Range(0, parts).Select(i => i < odd ? share + 1 : share)];
    }
}
