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

    /// <summary>
    /// What span of time each measurement covers. Left out: `PerPeriod` when adding, unchanged when updating. Can change
    /// only while the KPI has no measurements.
    /// </summary>
    public KpiWindowRequest? Window { get; set; }

    /// <summary>
    /// Read the KPI as the average of this many of its latest measurements, 2 to 12, instead of the latest alone — as
    /// velocity is read over the last 7 sprints. Only on a `PerPeriod` KPI. Left out: the latest alone.
    /// </summary>
    public int? RollingAverageOf { get; set; }
}

/// <summary>
/// What span of time each of a KPI's measurements covers.
/// </summary>
public sealed record KpiWindowRequest
{
    /// <summary>
    /// `PerPeriod` (each measurement gives its own period), `Cumulative` (from `anchorDate` to the day a value is taken
    /// as of), `Rolling` (`rollingCount` `rollingUnit`s ending on that day) or `PointInTime` (the state at one moment).
    /// </summary>
    public required KpiWindowKind Kind { get; set; }

    /// <summary>For `Cumulative` only: the first day every measurement covers, as `YYYY-MM-DD`. Fixed once measured.</summary>
    public LocalDate? AnchorDate { get; set; }

    /// <summary>For `Rolling` only: how many units each measurement covers, at least 1.</summary>
    public int? RollingCount { get; set; }

    /// <summary>For `Rolling` only: what `rollingCount` counts — `Days`.</summary>
    public KpiRollingUnit? RollingUnit { get; set; }

    /// <summary>The window as the commands take it.</summary>
    public KpiWindowInput ToInput() => new(Kind, AnchorDate, RollingCount, RollingUnit);
}

/// <summary>
/// A measurement's value and when it was taken, given the one way the KPI's window takes it: `periodStart` and
/// `periodEnd` for `PerPeriod`, `asOf` for `Cumulative` and `Rolling`, `measuredAt` for `PointInTime`.
/// </summary>
public sealed record KpiMeasurementRequest
{
    /// <summary>The raw measured value — never a percentage worked out from it.</summary>
    public required double Value { get; set; }

    /// <summary>For a `PerPeriod` KPI: the first day the value describes, as `YYYY-MM-DD`.</summary>
    public LocalDate? PeriodStart { get; set; }

    /// <summary>For a `PerPeriod` KPI: the last day the value describes. Periods on one KPI cannot overlap.</summary>
    public LocalDate? PeriodEnd { get; set; }

    /// <summary>
    /// For a `Cumulative` or `Rolling` KPI: the last day the value covers, as `YYYY-MM-DD`. Its period is worked out
    /// from the KPI's window; one value per day.
    /// </summary>
    public LocalDate? AsOf { get; set; }

    /// <summary>
    /// For a `PointInTime` KPI: the exact moment the value describes, as an ISO 8601 instant such as
    /// `2026-03-06T14:30:00Z`. Several a day are allowed, one per moment.
    /// </summary>
    public Instant? MeasuredAt { get; set; }

    /// <summary>For a `PerPeriod` KPI: the kind of record the period covers — `Sprint`, `PlanningInterval` or `Release` — given with `periodRecordId`, or null.</summary>
    public KpiPeriodRecordKind? PeriodRecordKind { get; set; }

    /// <summary>The id of the sprint, planning interval or release the period covers, or null.</summary>
    public Guid? PeriodRecordId { get; set; }

    /// <summary>The day the value was taken; when left out, the last day it covers (for a moment, its day in the system time zone).</summary>
    public LocalDate? MeasuredOn { get; set; }

    /// <summary>How many items the value was computed over, when known.</summary>
    public int? SampleSize { get; set; }

    /// <summary>Context for the value, up to 1,024 characters.</summary>
    public string? Note { get; set; }

    /// <summary>The measurement as the commands take it.</summary>
    public KpiMeasurementValuesInput ToInput() =>
        new(Value, PeriodStart, PeriodEnd, PeriodRecordKind, PeriodRecordId, MeasuredOn, SampleSize, Note, AsOf, MeasuredAt);
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

/// <summary>One checkpoint of a KPI's plan.</summary>
public sealed record KpiCheckpointRequest
{
    /// <summary>The day the planned value is due, as `YYYY-MM-DD`; one checkpoint per day.</summary>
    public required LocalDate Date { get; set; }

    /// <summary>A short name for it, such as `Q1` or `Sprint 12`, up to 32 characters.</summary>
    public required string Label { get; set; }

    /// <summary>The value the KPI is expected to reach by then. Planned values may fall back, for a seasonal plan.</summary>
    public required double PlannedValue { get; set; }

    /// <summary>
    /// Optional: a value short of the planned value, in the KPI's direction, past which a measurement read against
    /// this checkpoint is at risk rather than unhealthy. Leave it out for on-plan-or-not health.
    /// </summary>
    public double? AtRiskValue { get; set; }

    /// <summary>The checkpoint as the commands take it.</summary>
    public KpiCheckpointValues ToValues() => new(Date, Label, PlannedValue, AtRiskValue);
}

/// <summary>A KPI's whole checkpoint plan, replacing the one it has.</summary>
public sealed record KpiCheckpointPlanRequest
{
    /// <summary>Every checkpoint of the plan, in any order. Empty clears the plan.</summary>
    public List<KpiCheckpointRequest> Checkpoints { get; set; } = [];
}

/// <summary>Rejects a plan whose list or rows cannot be read, before the controller turns them into checkpoints.</summary>
public sealed class KpiCheckpointPlanRequestValidator : CustomValidator<KpiCheckpointPlanRequest>
{
    public KpiCheckpointPlanRequestValidator()
    {
        RuleFor(r => r.Checkpoints).NotNull();
        RuleForEach(r => r.Checkpoints).NotNull().ChildRules(checkpoint =>
        {
            checkpoint.RuleFor(c => c.Label).NotEmpty().MaximumLength(KpiCheckpointPlan.LabelMaxLength);
        });
    }
}
