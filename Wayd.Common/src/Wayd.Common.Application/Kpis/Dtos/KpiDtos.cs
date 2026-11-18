using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis.Dtos;

/// <summary>
/// What a KPI is measured against, as entered and as it resolves today.
/// </summary>
/// <param name="Direction">Whether the KPI should go up or down.</param>
/// <param name="BaselineValue">Where it started, or null when it has not been measured yet.</param>
/// <param name="BaselineWindowStart">The first day the baseline was taken over, when known.</param>
/// <param name="BaselineWindowEnd">The last day the baseline was taken over, when known.</param>
/// <param name="TargetKind">How the target is stated.</param>
/// <param name="TargetValue">The value to reach, or the percentage change from the baseline.</param>
/// <param name="AtRiskKind">How the at-risk threshold is stated, or null when there is none.</param>
/// <param name="AtRiskValue">The threshold value, or the percentage of the way to the target.</param>
/// <param name="ResolvedTargetValue">The value the target comes to, or null when it is relative and there is no baseline.</param>
/// <param name="ResolvedAtRiskValue">The value the threshold comes to, or null when there is none.</param>
public sealed record KpiGoalDto(
    KpiTargetDirection Direction,
    double? BaselineValue,
    LocalDate? BaselineWindowStart,
    LocalDate? BaselineWindowEnd,
    KpiTargetKind TargetKind,
    double TargetValue,
    KpiAtRiskKind? AtRiskKind,
    double? AtRiskValue,
    double? ResolvedTargetValue,
    double? ResolvedAtRiskValue)
{
    /// <summary>The goal as the KPI holds it.</summary>
    public static KpiGoalDto From(KpiGoal goal) => new(
        goal.Direction,
        goal.Baseline.Value,
        goal.Baseline.Window?.Start,
        goal.Baseline.Window?.End,
        goal.Target.Kind,
        goal.Target.Value,
        goal.AtRisk?.Kind,
        goal.AtRisk?.Value,
        goal.TargetValue,
        goal.AtRiskValue);
}

/// <summary>
/// What a KPI's latest value means against its goal, worked out when read.
/// </summary>
/// <param name="Value">The latest raw value, or null when nothing has been measured.</param>
/// <param name="ChangeFromBaseline">The latest value less the baseline, or null without both.</param>
/// <param name="ChangeFromBaselinePercent">That change as a percentage of the baseline's magnitude; null without both or from a baseline of zero.</param>
/// <param name="ProgressToTarget">How far the value has moved from the baseline toward the target, where 1 is the target reached; null without a baseline.</param>
/// <param name="Health">Whether the value meets the target, is at risk, or neither; null when nothing has been measured.</param>
/// <param name="Trend">Whether the latest value moved the right way from the one before it.</param>
public sealed record KpiReadingDto(
    double? Value,
    double? ChangeFromBaseline,
    double? ChangeFromBaselinePercent,
    double? ProgressToTarget,
    KpiHealth? Health,
    KpiTrend Trend)
{
    /// <summary>The reading as worked out.</summary>
    public static KpiReadingDto From(KpiReading reading) => new(
        reading.Value,
        reading.ChangeFromBaseline,
        reading.ChangeFromBaselinePercent,
        reading.ProgressToTarget,
        reading.Health,
        reading.Trend);
}

/// <summary>One value in a KPI's history, for a trend line.</summary>
/// <param name="PeriodStart">The first day the value describes.</param>
/// <param name="PeriodEnd">The last day the value describes.</param>
/// <param name="Value">The raw measured value.</param>
public sealed record KpiPointDto(LocalDate PeriodStart, LocalDate PeriodEnd, double Value);

/// <summary>A KPI category as a KPI names it.</summary>
/// <param name="Id">The category's id.</param>
/// <param name="Name">What it is called.</param>
/// <param name="Order">Its position, lowest first, which a scorecard groups by.</param>
public sealed record KpiCategoryRefDto(Guid Id, string Name, int Order);

/// <summary>A KPI category as the settings list shows it.</summary>
/// <param name="Id">The category's id.</param>
/// <param name="Name">What it is called.</param>
/// <param name="Description">What kind of KPI belongs in it.</param>
/// <param name="Order">Its position, lowest first.</param>
/// <param name="IsActive">Whether it is offered for new KPIs.</param>
/// <param name="KpiCount">How many KPIs, open and closed, have it; one with any cannot be deleted.</param>
public sealed record KpiCategoryDto(Guid Id, string Name, string? Description, int Order, bool IsActive, int KpiCount);

/// <summary>
/// One KPI as a scorecard lists it: what it is, its goal, its latest reading and its recent values.
/// </summary>
public sealed record KpiSummaryDto
{
    /// <summary>The KPI's id.</summary>
    public required Guid Id { get; init; }

    /// <summary>The KPI's key.</summary>
    public required int Key { get; init; }

    /// <summary>What it is called.</summary>
    public required string Name { get; init; }

    /// <summary>What it measures and why.</summary>
    public string? Description { get; init; }

    /// <summary>The category it is grouped under, or null when uncategorized.</summary>
    public KpiCategoryRefDto? Category { get; init; }

    /// <summary>A symbol shown before a value.</summary>
    public string? Prefix { get; init; }

    /// <summary>A unit shown after a value.</summary>
    public string? Suffix { get; init; }

    /// <summary>Where its measurements come from.</summary>
    public required KpiSource Source { get; init; }

    /// <summary>Which reading the scorecard shows.</summary>
    public required KpiExpressedAs ExpressedAs { get; init; }

    /// <summary>Whether it is on the scorecard.</summary>
    public required KpiStatus Status { get; init; }

    /// <summary>Its position on the scorecard, lowest first.</summary>
    public required int Order { get; init; }

    /// <summary>What it is measured against.</summary>
    public required KpiGoalDto Goal { get; init; }

    /// <summary>What its latest value means against the goal.</summary>
    public required KpiReadingDto Reading { get; init; }

    /// <summary>Its most recent values, oldest first, for a trend line.</summary>
    public required IReadOnlyList<KpiPointDto> RecentValues { get; init; }
}

/// <summary>
/// Every active KPI on one record, and what the caller may do with them.
/// </summary>
public sealed record KpiScorecardDto
{
    /// <summary>The record's registered subject type.</summary>
    public required string SubjectType { get; init; }

    /// <summary>The record's id.</summary>
    public required Guid SubjectId { get; init; }

    /// <summary>The record's name.</summary>
    public required string SubjectName { get; init; }

    /// <summary>The record's short code, when it has one.</summary>
    public string? SubjectCode { get; init; }

    /// <summary>Whether the caller may add, change and remove the record's KPIs and their measurements.</summary>
    public required bool CanManage { get; init; }

    /// <summary>The active KPIs, by category order (uncategorized last), then position, then key.</summary>
    public required IReadOnlyList<KpiSummaryDto> Kpis { get; init; }
}

/// <summary>One measurement as a KPI's history lists it.</summary>
public sealed record KpiMeasurementDto
{
    /// <summary>The measurement's id.</summary>
    public required Guid Id { get; init; }

    /// <summary>The raw measured value.</summary>
    public required double Value { get; init; }

    /// <summary>The first day the value describes.</summary>
    public required LocalDate PeriodStart { get; init; }

    /// <summary>The last day the value describes.</summary>
    public required LocalDate PeriodEnd { get; init; }

    /// <summary>The kind of record the period covers, if any.</summary>
    public KpiPeriodRecordKind? PeriodRecordKind { get; init; }

    /// <summary>The sprint, planning interval or release the period covers, if any.</summary>
    public Guid? PeriodRecordId { get; init; }

    /// <summary>The day the value was taken.</summary>
    public required LocalDate MeasuredOn { get; init; }

    /// <summary>The employee who took it, when known.</summary>
    public EmployeeNavigationDto? MeasuredBy { get; init; }

    /// <summary>How the value was arrived at.</summary>
    public required KpiMeasurementSource Source { get; init; }

    /// <summary>How many items the value was computed over, when known.</summary>
    public int? SampleSize { get; init; }

    /// <summary>Context the person recording it added.</summary>
    public string? Note { get; init; }

    /// <summary>The measurement, naming whoever took it from those given.</summary>
    public static KpiMeasurementDto From(KpiMeasurement measurement, IReadOnlyDictionary<Guid, EmployeeNavigationDto> employees) => new()
    {
        Id = measurement.Id,
        Value = measurement.Value,
        PeriodStart = measurement.PeriodStart,
        PeriodEnd = measurement.PeriodEnd,
        PeriodRecordKind = measurement.PeriodRecordKind,
        PeriodRecordId = measurement.PeriodRecordId,
        MeasuredOn = measurement.MeasuredOn,
        MeasuredBy = measurement.MeasuredById is { } id ? employees.GetValueOrDefault(id) : null,
        Source = measurement.Source,
        SampleSize = measurement.SampleSize,
        Note = measurement.Note,
    };
}

/// <summary>
/// One KPI with every measurement taken, newest first, and what the caller may do with it.
/// </summary>
public sealed record KpiDetailsDto
{
    /// <summary>The KPI as a scorecard lists it.</summary>
    public required KpiSummaryDto Kpi { get; init; }

    /// <summary>The record's name.</summary>
    public required string SubjectName { get; init; }

    /// <summary>Whether the caller may change the KPI and its measurements.</summary>
    public required bool CanManage { get; init; }

    /// <summary>Every measurement, newest period first.</summary>
    public required IReadOnlyList<KpiMeasurementDto> Measurements { get; init; }
}
