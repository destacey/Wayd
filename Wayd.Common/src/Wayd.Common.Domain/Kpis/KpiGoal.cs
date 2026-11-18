using Wayd.Common.Domain.Enums;

namespace Wayd.Common.Domain.Kpis;

/// <summary>
/// Where a KPI started: the value it was measured at before it was set, and optionally the days that value was taken
/// over.
/// </summary>
/// <param name="Value">The baseline value, or null when the KPI has not been measured yet.</param>
/// <param name="Window">The days the baseline was taken over. Only meaningful with a value.</param>
public sealed record KpiBaseline(double? Value, KpiPeriod? Window)
{
    /// <summary>A KPI with no baseline yet.</summary>
    public static KpiBaseline None { get; } = new(null, null);
}

/// <summary>
/// What a KPI aims for, as entered: a value to reach, or a percentage change from the baseline.
/// </summary>
/// <remarks>
/// Stored as the rule rather than the value it resolves to, so a relative target follows a corrected baseline.
/// </remarks>
/// <param name="Kind">How the target is stated.</param>
/// <param name="Value">
/// The value to reach for <see cref="KpiTargetKind.Absolute"/>; the percentage change, such as 25 for 25%, for
/// <see cref="KpiTargetKind.RelativeChange"/>.
/// </param>
public sealed record KpiTarget(KpiTargetKind Kind, double Value)
{
    /// <summary>A value to reach.</summary>
    public static KpiTarget Absolute(double value) => new(KpiTargetKind.Absolute, value);

    /// <summary>A percentage change from the baseline in the KPI's direction, such as 25 for "improve 25%".</summary>
    public static KpiTarget RelativeChange(double percent) => new(KpiTargetKind.RelativeChange, percent);

    /// <summary>
    /// The value this target resolves to, or null when it is relative and there is no baseline.
    /// </summary>
    /// <remarks>
    /// The change is taken from the baseline's magnitude, so "decrease 25%" moves a negative baseline further below
    /// zero rather than toward it.
    /// </remarks>
    public double? Resolve(double? baseline, KpiTargetDirection direction) => Kind switch
    {
        KpiTargetKind.Absolute => Value,
        KpiTargetKind.RelativeChange when baseline is { } b => b + Sign(direction) * Math.Abs(b) * Value / 100d,
        KpiTargetKind.RelativeChange => null,
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unsupported target kind."),
    };

    private static int Sign(KpiTargetDirection direction) => direction switch
    {
        KpiTargetDirection.Increase => 1,
        KpiTargetDirection.Decrease => -1,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unsupported target direction."),
    };
}

/// <summary>
/// The optional line short of a KPI's target past which a measurement is at risk rather than unhealthy.
/// </summary>
/// <remarks>
/// Without one, health has two states: on target or not. It stands in for a checkpoint plan, so a KPI aiming at a
/// value a year away does not read as unhealthy for the whole year.
/// </remarks>
/// <param name="Kind">How the threshold is stated.</param>
/// <param name="Value">
/// The threshold value for <see cref="KpiAtRiskKind.Absolute"/>; the percentage of the way from baseline to target,
/// such as 75, for <see cref="KpiAtRiskKind.ProgressToTarget"/>.
/// </param>
public sealed record KpiAtRiskThreshold(KpiAtRiskKind Kind, double Value)
{
    /// <summary>A threshold value.</summary>
    public static KpiAtRiskThreshold Absolute(double value) => new(KpiAtRiskKind.Absolute, value);

    /// <summary>A percentage of the way from the baseline to the target, such as 75.</summary>
    public static KpiAtRiskThreshold ProgressToTarget(double percent) => new(KpiAtRiskKind.ProgressToTarget, percent);

    /// <summary>
    /// The value this threshold resolves to, or null when it is stated as progress and there is no baseline or target.
    /// </summary>
    public double? Resolve(double? baseline, double? target) => Kind switch
    {
        KpiAtRiskKind.Absolute => Value,
        KpiAtRiskKind.ProgressToTarget when baseline is { } b && target is { } t => b + (t - b) * Value / 100d,
        KpiAtRiskKind.ProgressToTarget => null,
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unsupported at-risk kind."),
    };
}

/// <summary>
/// Everything a KPI is measured against, validated together because each part constrains the others.
/// </summary>
/// <param name="Direction">Whether the KPI should go up or down.</param>
/// <param name="Baseline">Where the KPI started.</param>
/// <param name="Target">What it aims for.</param>
/// <param name="AtRisk">The optional at-risk threshold.</param>
public sealed record KpiGoal(KpiTargetDirection Direction, KpiBaseline Baseline, KpiTarget Target, KpiAtRiskThreshold? AtRisk)
{
    /// <summary>The value the target resolves to, or null when it is relative and there is no baseline.</summary>
    public double? TargetValue => Target.Resolve(Baseline.Value, Direction);

    /// <summary>The value the at-risk threshold resolves to, or null when there is none or it cannot resolve.</summary>
    public double? AtRiskValue => AtRisk?.Resolve(Baseline.Value, TargetValue);

    /// <summary>
    /// Why this goal cannot be held, or null when it can.
    /// </summary>
    public string? Validate()
    {
        if (!Enum.IsDefined(Direction))
            return $"Unsupported target direction: {Direction}.";

        if (Baseline.Value is { } baselineValue && !double.IsFinite(baselineValue))
            return KpiErrors.NotAFiniteNumber("baseline");

        if (Baseline.Window is not null && Baseline.Value is null)
            return KpiErrors.BaselineWindowWithoutValue;

        if (!Enum.IsDefined(Target.Kind))
            return $"Unsupported target kind: {Target.Kind}.";

        if (!double.IsFinite(Target.Value))
            return KpiErrors.NotAFiniteNumber("target");

        if (Target.Kind == KpiTargetKind.RelativeChange)
        {
            if (Baseline.Value is not { } b)
                return KpiErrors.BaselineRequired;
            if (b == 0)
                return KpiErrors.RelativeChangeFromZero;
            if (Target.Value <= 0)
                return KpiErrors.RelativeChangeMustBePositive;
        }

        var target = TargetValue!.Value;

        if (Baseline.Value is { } start)
        {
            if (Direction == KpiTargetDirection.Increase && start >= target)
                return KpiErrors.BaselineMeetsTargetIncrease;
            if (Direction == KpiTargetDirection.Decrease && start <= target)
                return KpiErrors.BaselineMeetsTargetDecrease;
        }

        if (AtRisk is null)
            return null;

        if (!Enum.IsDefined(AtRisk.Kind))
            return $"Unsupported at-risk kind: {AtRisk.Kind}.";

        if (!double.IsFinite(AtRisk.Value))
            return KpiErrors.NotAFiniteNumber("at-risk threshold");

        if (AtRisk.Kind == KpiAtRiskKind.ProgressToTarget)
        {
            if (Baseline.Value is null)
                return KpiErrors.BaselineRequired;
            if (AtRisk.Value <= 0 || AtRisk.Value >= 100)
                return KpiErrors.AtRiskProgressOutOfRange;
        }

        var atRisk = AtRiskValue!.Value;

        if (Direction == KpiTargetDirection.Increase && atRisk >= target)
            return KpiErrors.AtRiskNotShortOfTargetIncrease;
        if (Direction == KpiTargetDirection.Decrease && atRisk <= target)
            return KpiErrors.AtRiskNotShortOfTargetDecrease;

        return null;
    }
}
