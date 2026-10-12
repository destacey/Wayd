namespace Wayd.Work.Domain.Models.SprintMetrics;

/// <summary>One metric across a team's sprints: what the team typically gets, and how far its sprints stray from it.</summary>
/// <param name="Average">The mean of the sprints' values, not a ratio of their pooled totals.</param>
/// <param name="Min">The lowest sprint's value.</param>
/// <param name="Max">The highest sprint's value.</param>
/// <param name="StandardDeviation">
/// The population standard deviation of the sprints' values: the sprints are the whole window, not a sample of it.
/// </param>
/// <param name="Sprints">How many sprints had a value. Fewer than the window when a sprint has none for this metric.</param>
public sealed record SprintMetricSummary(double Average, double Min, double Max, double StandardDeviation, int Sprints)
{
    /// <summary>Summarizes <paramref name="values"/>, skipping sprints with none; null when no sprint has one.</summary>
    public static SprintMetricSummary? Of(IEnumerable<double?> values)
    {
        var list = values.OfType<double>().ToList();
        if (list.Count == 0)
            return null;

        var average = list.Average();
        var variance = list.Sum(v => (v - average) * (v - average)) / list.Count;

        return new SprintMetricSummary(average, list.Min(), list.Max(), Math.Sqrt(variance), list.Count);
    }
}
