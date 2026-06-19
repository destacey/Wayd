namespace Wayd.Work.Application.WorkItems.Dtos;

/// <summary>
/// Cycle-time rollup over a set of completed work items.
///
/// Carries the underlying count and total so consumers can aggregate across
/// scopes without average-of-averages bias: combining two summaries is
/// <c>{ Count = a.Count + b.Count, TotalDays = a.TotalDays + b.TotalDays }</c>,
/// from which the correct weighted average is <c>TotalDays / Count</c>.
/// </summary>
public sealed record CycleTimeSummary
{
    /// <summary>
    /// Number of completed work items that contributed to this summary
    /// (i.e. items with both an Activated and Done timestamp where
    /// Done &gt; Activated).
    /// </summary>
    public int WorkItemsCount { get; init; }

    /// <summary>
    /// Sum of cycle times in days across the contributing items.
    /// When <see cref="WorkItemsCount"/> is 0 this is the identity value 0
    /// (the sum of an empty set), not a missing value — UI should treat
    /// "no data" as <c>WorkItemsCount == 0</c> rather than reading this field.
    /// </summary>
    public double TotalCycleTimeDays { get; init; }

    /// <summary>
    /// Convenience-only mean (TotalCycleTimeDays / WorkItemsCount), or null when
    /// there are no contributing items. Aggregating consumers should compose
    /// new summaries from <see cref="WorkItemsCount"/> and
    /// <see cref="TotalCycleTimeDays"/> rather than averaging this value.
    /// </summary>
    public double? AverageCycleTimeDays => WorkItemsCount > 0
        ? TotalCycleTimeDays / WorkItemsCount
        : null;

    public static CycleTimeSummary Empty { get; } = new();

    public static CycleTimeSummary Combine(IEnumerable<CycleTimeSummary> summaries)
    {
        var count = 0;
        var total = 0d;
        foreach (var s in summaries)
        {
            count += s.WorkItemsCount;
            total += s.TotalCycleTimeDays;
        }
        return new CycleTimeSummary { WorkItemsCount = count, TotalCycleTimeDays = total };
    }
}
