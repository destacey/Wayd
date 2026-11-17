using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Models.KeyPerformanceIndicators;

namespace Wayd.Common.Domain.Kpis;

/// <summary>
/// What a KPI's latest value means against its goal: the change from baseline, the progress to target, its health and
/// its trend.
/// </summary>
/// <remarks>
/// Worked out on read and never stored, from the raw value and the goal as they are now, so a corrected baseline or a
/// moved target reads through at once. A pure function of values, so a query can read the latest two measurements in
/// SQL and build it without loading the aggregate.
/// </remarks>
/// <param name="Value">The latest raw value, or null when nothing has been measured.</param>
/// <param name="TargetValue">The value the target resolves to, or null when it cannot resolve.</param>
/// <param name="AtRiskValue">The value the at-risk threshold resolves to, or null when there is none.</param>
/// <param name="ChangeFromBaseline">The latest value less the baseline, or null without both.</param>
/// <param name="ChangeFromBaselinePercent">That change as a percentage of the baseline's magnitude, or null without both or from a baseline of zero.</param>
/// <param name="ProgressToTarget">How far the value has moved from the baseline toward the target, where 1 is the target reached; null without a baseline.</param>
/// <param name="Health">Whether the value meets the target, is at risk, or neither; null when nothing has been measured.</param>
/// <param name="Trend">Whether the latest value moved the right way from the one before it.</param>
public sealed record KpiReading(
    double? Value,
    double? TargetValue,
    double? AtRiskValue,
    double? ChangeFromBaseline,
    double? ChangeFromBaselinePercent,
    double? ProgressToTarget,
    KpiHealth? Health,
    KpiTrend Trend)
{
    /// <summary>
    /// Reads a KPI's latest value against its goal.
    /// </summary>
    /// <param name="goal">What the KPI is measured against.</param>
    /// <param name="latest">The latest raw value, or null when nothing has been measured.</param>
    /// <param name="previous">The value before it, or null when there is only one.</param>
    public static KpiReading Of(KpiGoal goal, double? latest, double? previous)
    {
        ArgumentNullException.ThrowIfNull(goal);

        var target = goal.TargetValue;
        var atRisk = goal.AtRiskValue;
        var baseline = goal.Baseline.Value;

        if (latest is not { } value)
            return new KpiReading(null, target, atRisk, null, null, null, null, KpiTrend.NoData);

        double? change = baseline is { } b ? value - b : null;
        double? changePercent = baseline is { } b2 && b2 != 0 ? (value - b2) / Math.Abs(b2) * 100d : null;
        double? progress = target is { } t ? KpiUtils.CalculateProgress(baseline, value, t, goal.Direction) : null;
        KpiHealth? health = target is { } t2 ? KpiUtils.GetKpiHealth(value, t2, atRisk, goal.Direction) : null;
        var trend = KpiUtils.GetKpiTrend(previous, value, goal.Direction);

        return new KpiReading(value, target, atRisk, change, changePercent, progress, health, trend);
    }
}
