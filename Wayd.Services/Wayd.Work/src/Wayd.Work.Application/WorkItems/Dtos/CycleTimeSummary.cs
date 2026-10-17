namespace Wayd.Work.Application.WorkItems.Dtos;

/// <summary>
/// Cycle-time rollup over a set of completed work items.
/// Carries the underlying count and total so consumers can aggregate across
/// scopes without average-of-averages bias: sum <c>workItemsCount</c> and
/// <c>totalCycleTimeDays</c> across summaries, then divide the total by the count.
/// </summary>
public sealed record CycleTimeSummary
{
    /// <summary>
    /// Number of completed work items that contributed to this summary: items
    /// with both an activated and a done timestamp, done after activated.
    /// </summary>
    public int WorkItemsCount { get; init; }

    /// <summary>
    /// Sum of cycle times in days across the contributing items.
    /// 0 when <c>workItemsCount</c> is 0; treat that count, not this field,
    /// as the sign that there is no data.
    /// </summary>
    public double TotalCycleTimeDays { get; init; }

    /// <summary>
    /// Mean cycle time in days, or null when there are no contributing items.
    /// To combine summaries, sum <c>workItemsCount</c> and <c>totalCycleTimeDays</c>
    /// rather than averaging this value.
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
