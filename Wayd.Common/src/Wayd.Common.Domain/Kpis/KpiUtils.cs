using Wayd.Common.Domain.Enums;

namespace Wayd.Common.Domain.Kpis;

/// <summary>
/// The arithmetic every KPI reading shares: whether a value is on track, its health against a target and an
/// optional at-risk value, its trend against the previous value, and its progress from a baseline.
/// </summary>
public static class KpiUtils
{
    /// <summary>
    /// Determines whether a key performance indicator (KPI) is on track based on the actual value, the target value,
    /// and the specified target direction.
    /// </summary>
    /// <param name="actualValue">The current value of the KPI to evaluate.</param>
    /// <param name="targetValue">The target value against which the actual KPI value is compared.</param>
    /// <param name="targetDirection">The direction in which the KPI is expected to move to be considered on track. Specify whether the KPI should
    /// increase or decrease to meet the target.</param>
    /// <returns>true if the actual value meets the criteria for being on track according to the target value and direction;
    /// otherwise, false.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the specified target direction is not supported.</exception>
    public static bool IsKpiOnTrack(double actualValue, double targetValue, KpiTargetDirection targetDirection)
    {
        return targetDirection switch
        {
            KpiTargetDirection.Increase => actualValue >= targetValue,
            KpiTargetDirection.Decrease => actualValue <= targetValue,
            _ => throw new ArgumentOutOfRangeException(nameof(targetDirection), $"Unsupported target direction: {targetDirection}")
        };
    }

    /// <summary>
    /// Determines the health of a KPI measurement relative to its target and optional at-risk threshold.
    /// Returns Healthy when the target is met, AtRisk when between the at-risk value and target, or Unhealthy otherwise.
    /// When <paramref name="atRiskValue"/> is null, only Healthy and Unhealthy are returned.
    /// </summary>
    /// <param name="actualValue">The current value of the KPI to evaluate.</param>
    /// <param name="targetValue">The target value against which the actual KPI value is compared.</param>
    /// <param name="atRiskValue">The optional at-risk threshold value for the KPI.</param>
    /// <param name="targetDirection">The direction in which the KPI is expected to move to be considered on track.</param>
    /// <returns>The health status of the KPI.</returns>
    public static KpiHealth GetKpiHealth(double actualValue, double targetValue, double? atRiskValue, KpiTargetDirection targetDirection)
    {
        if (IsKpiOnTrack(actualValue, targetValue, targetDirection))
            return KpiHealth.Healthy;

        if (atRiskValue.HasValue && IsKpiOnTrack(actualValue, atRiskValue.Value, targetDirection))
            return KpiHealth.AtRisk;

        return KpiHealth.Unhealthy;
    }

    /// <summary>
    /// Determines the trend of a Key Performance Indicator (KPI) by comparing the current value to the previous value,
    /// taking into account the specified target direction for improvement.
    /// </summary>
    /// <remarks>If previousValue is null, the method returns KpiTrend.NoData to indicate that a trend cannot
    /// be established. Use this method to evaluate KPI performance in scenarios where the direction of improvement may
    /// vary depending on the metric.</remarks>
    /// <param name="previousValue">The previous KPI value to compare against. If null, indicates that no prior data is available and a trend cannot
    /// be determined.</param>
    /// <param name="currentValue">The current KPI value used to assess the trend relative to the previous value.</param>
    /// <param name="targetDirection">The desired direction of improvement for the KPI. Specifies whether an increase or decrease in value is
    /// considered favorable.</param>
    /// <returns>A value of the KpiTrend enumeration that indicates whether the KPI is improving, worsening, stable, or if no
    /// data is available to determine the trend.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the specified targetDirection is not a valid value of the KpiTargetDirection enumeration.</exception>
    public static KpiTrend GetKpiTrend(double? previousValue, double currentValue, KpiTargetDirection targetDirection)
    {
        if (!previousValue.HasValue)
            return KpiTrend.NoData;

        return targetDirection switch
        {
            KpiTargetDirection.Increase => currentValue > previousValue.Value ? KpiTrend.Improving : (currentValue < previousValue.Value ? KpiTrend.Worsening : KpiTrend.Stable),
            KpiTargetDirection.Decrease => currentValue < previousValue.Value ? KpiTrend.Improving : (currentValue > previousValue.Value ? KpiTrend.Worsening : KpiTrend.Stable),
            _ => throw new ArgumentOutOfRangeException(nameof(targetDirection), $"Unsupported target direction: {targetDirection}")
        };
    }

    /// <summary>
    /// Calculates the progress towards a KPI target based on the starting value, actual value, target value, and target direction.
    /// </summary>
    /// <param name="startingValue">The starting (baseline) value of the KPI.</param>
    /// <param name="actualValue">The current actual value of the KPI.</param>
    /// <param name="targetValue">The target value that defines success for the KPI.</param>
    /// <param name="targetDirection">The desired direction of improvement for the KPI.</param>
    /// <returns>The progress towards the KPI target as a percentage, or null if the starting value is not provided.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the specified targetDirection is not a valid value of the KpiTargetDirection enumeration.</exception>
    public static double? CalculateProgress(double? startingValue, double? actualValue, double targetValue, KpiTargetDirection targetDirection)
    {
        if (!startingValue.HasValue) return null;
        if (!actualValue.HasValue) return 0;

        // Guard against division by zero when starting value equals target value
        if (startingValue.Value == targetValue) return null;

        double progress = targetDirection switch
        {
            KpiTargetDirection.Increase => (actualValue.Value - startingValue.Value) / (targetValue - startingValue.Value),
            KpiTargetDirection.Decrease => (startingValue.Value - actualValue.Value) / (startingValue.Value - targetValue),
            _ => throw new ArgumentOutOfRangeException(nameof(targetDirection), $"Unsupported target direction: {targetDirection}")
        };

        return progress;
    }
}
