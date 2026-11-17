using Wayd.Common.Application.Kpis;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Web.Api.Models.Kpis;

/// <summary>
/// Where a KPI started.
/// </summary>
public sealed record KpiBaselineRequest
{
    /// <summary>The baseline value, or null when the KPI has not been measured yet. Required for a `RelativeChange` target.</summary>
    public double? Value { get; set; }

    /// <summary>The first day the baseline was taken over, as `YYYY-MM-DD`. Give both ends of the window or neither.</summary>
    public LocalDate? WindowStart { get; set; }

    /// <summary>The last day the baseline was taken over, as `YYYY-MM-DD`.</summary>
    public LocalDate? WindowEnd { get; set; }

    /// <summary>The baseline as the commands take it.</summary>
    public KpiBaselineInput ToInput() => new(Value, WindowStart, WindowEnd);
}

/// <summary>
/// What a KPI aims for.
/// </summary>
public sealed record KpiTargetRequest
{
    /// <summary>Whether the KPI should go up (`Increase`) or down (`Decrease`).</summary>
    public required KpiTargetDirection Direction { get; set; }

    /// <summary>`Absolute` for a value to reach, or `RelativeChange` for a percentage change from the baseline in the KPI's direction.</summary>
    public KpiTargetKind Kind { get; set; } = KpiTargetKind.Absolute;

    /// <summary>The value to reach, or the percentage change — 25 for 25% — for `RelativeChange`.</summary>
    public required double Value { get; set; }

    /// <summary>
    /// How the optional at-risk threshold is stated: `Absolute` for a value, or `ProgressToTarget` for a percentage of
    /// the way from the baseline to the target. Leave it and `atRiskValue` out for no threshold.
    /// </summary>
    public KpiAtRiskKind? AtRiskKind { get; set; }

    /// <summary>The threshold value, or the percentage of the way to the target. A value past it but short of the target is at risk.</summary>
    public double? AtRiskValue { get; set; }

    /// <summary>The target as the commands take it.</summary>
    public KpiTargetInput ToInput() => new(Direction, Kind, Value, AtRiskKind, AtRiskValue);
}

/// <summary>
/// Everything about a KPI that is entered rather than measured: its descriptive fields, where it started and what it
/// aims for.
/// </summary>
public sealed record KpiRequest
{
    /// <summary>What the KPI is called, up to 128 characters.</summary>
    public required string Name { get; set; }

    /// <summary>What it measures and why, up to 1,024 characters.</summary>
    public string? Description { get; set; }

    /// <summary>An active KPI category to group it under, or null for none.</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>A symbol shown before a value, such as `$`, up to 16 characters.</summary>
    public string? Prefix { get; set; }

    /// <summary>A unit shown after a value, such as `days`, up to 16 characters.</summary>
    public string? Suffix { get; set; }

    /// <summary>Which reading the scorecard shows: `Value`, `ChangeFromBaseline` or `ProgressToTarget`.</summary>
    public KpiExpressedAs ExpressedAs { get; set; } = KpiExpressedAs.Value;

    /// <summary>Where the KPI started.</summary>
    public KpiBaselineRequest Baseline { get; set; } = new();

    /// <summary>What it aims for.</summary>
    public required KpiTargetRequest Target { get; set; }
}

/// <summary>
/// A measurement's value and the period it describes.
/// </summary>
public sealed record KpiMeasurementRequest
{
    /// <summary>The raw measured value — never a percentage worked out from it.</summary>
    public required double Value { get; set; }

    /// <summary>The first day the value describes, as `YYYY-MM-DD`.</summary>
    public required LocalDate PeriodStart { get; set; }

    /// <summary>The last day the value describes. Periods on one KPI cannot overlap.</summary>
    public required LocalDate PeriodEnd { get; set; }

    /// <summary>The kind of record the period covers — `Sprint`, `PlanningInterval` or `Release` — given with `periodRecordId`, or null.</summary>
    public KpiPeriodRecordKind? PeriodRecordKind { get; set; }

    /// <summary>The id of the sprint, planning interval or release the period covers, or null.</summary>
    public Guid? PeriodRecordId { get; set; }

    /// <summary>The day the value was taken; the period's last day when left out.</summary>
    public LocalDate? MeasuredOn { get; set; }

    /// <summary>How many items the value was computed over, when known.</summary>
    public int? SampleSize { get; set; }

    /// <summary>Context for the value, up to 1,024 characters.</summary>
    public string? Note { get; set; }

    /// <summary>The measurement as the commands take it.</summary>
    public KpiMeasurementValuesInput ToInput() =>
        new(Value, PeriodStart, PeriodEnd, PeriodRecordKind, PeriodRecordId, MeasuredOn, SampleSize, Note);
}

/// <summary>A new order for a record's KPIs.</summary>
public sealed record ReorderKpisRequest
{
    /// <summary>Every KPI on the record, open and closed, each once, in their new order.</summary>
    public List<Guid> KpiIds { get; set; } = [];
}

/// <summary>A KPI category's name and description.</summary>
public sealed record KpiCategoryRequest
{
    /// <summary>What the category is called, up to 64 characters; unique, ignoring case.</summary>
    public string Name { get; set; } = default!;

    /// <summary>What kind of KPI belongs in it, up to 512 characters.</summary>
    public string? Description { get; set; }
}

/// <summary>A new order for the organization's KPI categories.</summary>
public sealed record ReorderKpiCategoriesRequest
{
    /// <summary>Every category, active and inactive, each once, in their new order.</summary>
    public List<Guid> CategoryIds { get; set; } = [];
}
