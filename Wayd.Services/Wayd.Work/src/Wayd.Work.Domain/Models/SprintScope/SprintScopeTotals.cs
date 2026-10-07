namespace Wayd.Work.Domain.Models.SprintScope;

/// <summary>How many items, and how much estimate, fall in one category. A missing estimate adds nothing.</summary>
public sealed record SprintScopeMeasure(int Count, double Estimate)
{
    /// <summary>The number of <paramref name="items"/> and the sum of their <paramref name="estimate"/>.</summary>
    public static SprintScopeMeasure Of(IEnumerable<SprintScopeItem> items, Func<SprintScopeItem, double?> estimate)
    {
        var list = items.ToList();
        return new SprintScopeMeasure(list.Count, list.Sum(i => estimate(i) ?? 0));
    }
}

/// <summary>
/// A sprint's scope summed by category. Committed and Added are measured with the estimate each item had
/// when it came in; the outcomes with the estimate it had when it was last in the sprint.
/// </summary>
/// <param name="Total">
/// Every item that was in scope, measured like the outcomes, so it is the sum of Completed, Carried Over,
/// Descoped and Remaining. By count it also equals Committed plus Added; by estimate it need not, since an
/// item re-estimated during the sprint comes in at one estimate and leaves at another.
/// </param>
/// <param name="Committed">Items in the sprint at its commitment point, at their estimate then.</param>
/// <param name="Added">Items that entered the sprint after its commitment point, at their estimate when added.</param>
/// <param name="Completed">Completed items, including those completed as Removed.</param>
/// <param name="Removed">The part of <paramref name="Completed"/> completed as Removed.</param>
/// <param name="CarriedOver">Unfinished items carried over to the team's next sprint.</param>
/// <param name="Descoped">Unfinished items taken out of the sprint.</param>
/// <param name="Remaining">Unfinished items still in a sprint that has not ended.</param>
/// <param name="CompletedOfCommitted">
/// Committed items that were completed, measured with their committed estimate so that it compares with
/// <paramref name="Committed"/>: re-estimating an item during the sprint does not move the say/do ratio.
/// </param>
/// <param name="SayDoCount">Completed of committed ÷ committed, by item count; null when nothing was committed.</param>
/// <param name="SayDoEstimate">Completed of committed ÷ committed, by estimate; null when no committed estimate.</param>
/// <param name="Unestimated">Items with no value in the sprint's estimate when they were last in the sprint.</param>
public sealed record SprintScopeTotals(
    SprintScopeMeasure Total,
    SprintScopeMeasure Committed,
    SprintScopeMeasure Added,
    SprintScopeMeasure Completed,
    SprintScopeMeasure Removed,
    SprintScopeMeasure CarriedOver,
    SprintScopeMeasure Descoped,
    SprintScopeMeasure Remaining,
    SprintScopeMeasure CompletedOfCommitted,
    double? SayDoCount,
    double? SayDoEstimate,
    int Unestimated)
{
    /// <summary>Sums <paramref name="items"/> by category.</summary>
    public static SprintScopeTotals Of(IReadOnlyCollection<SprintScopeItem> items)
    {
        var committed = items.Where(i => i.Entry == SprintScopeEntry.Committed).ToList();

        var committedMeasure = SprintScopeMeasure.Of(committed, i => i.EntryEstimate);
        var completedOfCommitted = SprintScopeMeasure.Of(committed.Where(i => i.IsCompleted), i => i.EntryEstimate);

        return new SprintScopeTotals(
            Total: SprintScopeMeasure.Of(items, i => i.OutcomeEstimate),
            Committed: committedMeasure,
            Added: SprintScopeMeasure.Of(items.Where(i => i.Entry == SprintScopeEntry.Added), i => i.EntryEstimate),
            Completed: SprintScopeMeasure.Of(items.Where(i => i.IsCompleted), i => i.OutcomeEstimate),
            Removed: SprintScopeMeasure.Of(items.Where(i => i.Outcome == SprintScopeOutcome.Removed), i => i.OutcomeEstimate),
            CarriedOver: SprintScopeMeasure.Of(items.Where(i => i.Outcome == SprintScopeOutcome.CarriedOver), i => i.OutcomeEstimate),
            Descoped: SprintScopeMeasure.Of(items.Where(i => i.Outcome == SprintScopeOutcome.Descoped), i => i.OutcomeEstimate),
            Remaining: SprintScopeMeasure.Of(items.Where(i => i.Outcome == SprintScopeOutcome.Remaining), i => i.OutcomeEstimate),
            CompletedOfCommitted: completedOfCommitted,
            SayDoCount: committedMeasure.Count == 0 ? null : (double)completedOfCommitted.Count / committedMeasure.Count,
            SayDoEstimate: committedMeasure.Estimate == 0 ? null : completedOfCommitted.Estimate / committedMeasure.Estimate,
            Unestimated: items.Count(i => i.OutcomeEstimate is null));
    }
}
