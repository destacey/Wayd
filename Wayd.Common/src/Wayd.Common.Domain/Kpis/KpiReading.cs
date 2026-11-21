using Wayd.Common.Domain.Enums;

namespace Wayd.Common.Domain.Kpis;

/// <summary>
/// What a KPI's latest value means against its goal: the change from baseline, the progress to target, its health and
/// its trend.
/// </summary>
/// <remarks>
/// Worked out on read and never stored, from the raw value and the goal as they are now, so a corrected baseline or a
/// moved target reads through at once. A pure function of values, so a query can read the latest two measurements in
/// SQL and build it without loading the aggregate.
/// <para>
/// With a checkpoint plan, health is judged against the checkpoint the value falls under — its planned value, and its
/// at-risk value or none — rather than the target. Progress and change are still taken toward the target.
/// </para>
/// </remarks>
/// <param name="Value">The latest raw value, or null when nothing has been measured.</param>
/// <param name="TargetValue">The value the target resolves to, or null when it cannot resolve.</param>
/// <param name="AtRiskValue">The value the KPI-level at-risk threshold resolves to, or null when there is none.</param>
/// <param name="ChangeFromBaseline">The latest value less the baseline, or null without both.</param>
/// <param name="ChangeFromBaselinePercent">That change as a percentage of the baseline's magnitude, or null without both or from a baseline of zero.</param>
/// <param name="ProgressToTarget">How far the value has moved from the baseline toward the target, where 1 is the target reached; null without a baseline.</param>
/// <param name="Health">
/// Whether the value meets its checkpoint's planned value, or the target past the plan, is at risk, or neither; null when
/// nothing has been measured.
/// </param>
/// <param name="Trend">Whether the latest value moved the right way from the one before it.</param>
/// <param name="LatestValue">The latest raw measurement, which is <paramref name="Value"/> unless the reading is a rolling average.</param>
/// <param name="AveragedCount">
/// How many measurements <paramref name="Value"/> averages, or null when it is the latest alone. Fewer than the KPI asks
/// for while it has fewer measurements than that.
/// </param>
/// <param name="Checkpoint">The checkpoint health was judged against, or null when it was judged against the target.</param>
public sealed record KpiReading(
    double? Value,
    double? TargetValue,
    double? AtRiskValue,
    double? ChangeFromBaseline,
    double? ChangeFromBaselinePercent,
    double? ProgressToTarget,
    KpiHealth? Health,
    KpiTrend Trend,
    double? LatestValue = null,
    int? AveragedCount = null,
    KpiCheckpointValues? Checkpoint = null)
{
    /// <summary>
    /// Reads a KPI's values against its goal: the latest alone, or the average of the latest
    /// <paramref name="rollingAverageOf"/> of them.
    /// </summary>
    /// <remarks>
    /// With a rolling average the average is what is held to the goal — a team's velocity is judged over its last
    /// seven sprints, not its last one — and the trend compares it with the average one measurement earlier.
    /// </remarks>
    /// <param name="goal">What the KPI is measured against.</param>
    /// <param name="newestFirst">Its values, newest first; at least <paramref name="rollingAverageOf"/> plus one to show a trend.</param>
    /// <param name="rollingAverageOf">How many to average, or null to read the latest alone.</param>
    /// <param name="checkpoint">
    /// The checkpoint the latest value falls under, which a rolling average is judged against too; null to judge
    /// against the target.
    /// </param>
    public static KpiReading OfValues(KpiGoal goal, IReadOnlyList<double> newestFirst, int? rollingAverageOf, KpiCheckpointValues? checkpoint = null)
    {
        ArgumentNullException.ThrowIfNull(newestFirst);

        var latest = newestFirst.Count > 0 ? newestFirst[0] : (double?)null;

        if (rollingAverageOf is not { } count)
        {
            var previous = newestFirst.Count > 1 ? newestFirst[1] : (double?)null;
            return Of(goal, latest, previous, checkpoint) with { LatestValue = latest };
        }

        var current = newestFirst.Take(count).ToList();
        var prior = newestFirst.Skip(1).Take(count).ToList();

        var reading = Of(goal, current.Count > 0 ? current.Average() : null, prior.Count > 0 ? prior.Average() : null, checkpoint);
        return reading with { LatestValue = latest, AveragedCount = current.Count > 0 ? current.Count : null };
    }

    /// <summary>
    /// Reads a KPI's latest value against its goal.
    /// </summary>
    /// <param name="goal">What the KPI is measured against.</param>
    /// <param name="latest">The latest raw value, or null when nothing has been measured.</param>
    /// <param name="previous">The value before it, or null when there is only one.</param>
    /// <param name="checkpoint">The checkpoint the latest value falls under, or null to judge it against the target.</param>
    public static KpiReading Of(KpiGoal goal, double? latest, double? previous, KpiCheckpointValues? checkpoint = null)
    {
        ArgumentNullException.ThrowIfNull(goal);

        var target = goal.TargetValue;
        var atRisk = goal.AtRiskValue;
        var baseline = goal.Baseline.Value;

        if (latest is not { } value)
            return new KpiReading(null, target, atRisk, null, null, null, null, KpiTrend.NoData, Checkpoint: checkpoint);

        double? change = baseline is { } b ? value - b : null;
        double? changePercent = baseline is { } b2 && b2 != 0 ? (value - b2) / Math.Abs(b2) * 100d : null;
        double? progress = target is { } t ? KpiUtils.CalculateProgress(baseline, value, t, goal.Direction) : null;
        KpiHealth? health = checkpoint is not null
            ? KpiUtils.GetKpiHealth(value, checkpoint.PlannedValue, checkpoint.AtRiskValue, goal.Direction)
            : target is { } t2 ? KpiUtils.GetKpiHealth(value, t2, atRisk, goal.Direction) : null;
        var trend = KpiUtils.GetKpiTrend(previous, value, goal.Direction);

        return new KpiReading(value, target, atRisk, change, changePercent, progress, health, trend, Checkpoint: checkpoint);
    }
}
