using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis;

/// <summary>
/// A KPI's baseline as a caller supplies it: a value and, optionally, the days it was taken over.
/// </summary>
/// <param name="Value">The baseline value, or null when the KPI has not been measured yet.</param>
/// <param name="WindowStart">The first day the baseline was taken over. Given with <paramref name="WindowEnd"/> or not at all.</param>
/// <param name="WindowEnd">The last day the baseline was taken over.</param>
public sealed record KpiBaselineInput(double? Value, LocalDate? WindowStart, LocalDate? WindowEnd)
{
    /// <summary>The baseline, failing when only one end of the window is given or it ends before it starts.</summary>
    public Result<KpiBaseline> ToBaseline()
    {
        if (WindowStart.HasValue != WindowEnd.HasValue)
            return Result.Failure<KpiBaseline>("A baseline window needs both a start and an end.");

        if (WindowStart is not { } start || WindowEnd is not { } end)
            return new KpiBaseline(Value, null);

        return KpiPeriod.Create(start, end).Map(window => new KpiBaseline(Value, window));
    }
}

/// <summary>
/// What a KPI aims for as a caller supplies it: its direction, its target and an optional at-risk threshold.
/// </summary>
/// <param name="Direction">Whether the KPI should go up or down.</param>
/// <param name="TargetKind">How the target is stated.</param>
/// <param name="TargetValue">The value to reach, or the percentage change from the baseline.</param>
/// <param name="AtRiskKind">How the at-risk threshold is stated, or null for none.</param>
/// <param name="AtRiskValue">The threshold value, or the percentage of the way to the target. Required with <paramref name="AtRiskKind"/>.</param>
public sealed record KpiTargetInput(
    KpiTargetDirection Direction,
    KpiTargetKind TargetKind,
    double TargetValue,
    KpiAtRiskKind? AtRiskKind,
    double? AtRiskValue)
{
    /// <summary>The target as entered.</summary>
    public KpiTarget ToTarget() => new(TargetKind, TargetValue);

    /// <summary>The at-risk threshold, failing when a kind is given without a value or a value without a kind.</summary>
    public Result<KpiAtRiskThreshold?> ToAtRisk()
    {
        if (AtRiskKind.HasValue != AtRiskValue.HasValue)
            return Result.Failure<KpiAtRiskThreshold?>("An at-risk threshold needs both how it is stated and its value.");

        return AtRiskKind is { } kind ? new KpiAtRiskThreshold(kind, AtRiskValue!.Value) : (KpiAtRiskThreshold?)null;
    }
}

/// <summary>
/// A measurement as a caller supplies it.
/// </summary>
/// <param name="Value">The raw measured value.</param>
/// <param name="PeriodStart">The first day the value describes.</param>
/// <param name="PeriodEnd">The last day the value describes.</param>
/// <param name="PeriodRecordKind">The kind of record the period covers, if any.</param>
/// <param name="PeriodRecordId">The sprint, planning interval or release the period covers, if any.</param>
/// <param name="MeasuredOn">The day the value was taken; the period's last day when not given.</param>
/// <param name="SampleSize">How many items the value was computed over, when known.</param>
/// <param name="Note">Context for the value.</param>
public sealed record KpiMeasurementValuesInput(
    double Value,
    LocalDate PeriodStart,
    LocalDate PeriodEnd,
    KpiPeriodRecordKind? PeriodRecordKind,
    Guid? PeriodRecordId,
    LocalDate? MeasuredOn,
    int? SampleSize,
    string? Note)
{
    /// <summary>The domain's input, recorded by hand by the given employee.</summary>
    public Result<KpiMeasurementInput> ToInput(Guid? measuredById) =>
        KpiPeriod.Create(PeriodStart, PeriodEnd).Map(period => new KpiMeasurementInput(
            Value, period, PeriodRecordKind, PeriodRecordId, MeasuredOn ?? PeriodEnd, measuredById,
            KpiMeasurementSource.Manual, SampleSize, Note));
}
