namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// The generated KPIs, keyed throughout by the generator's own handles. The seed area projects them to CSV rows,
/// substituting the team ids and category ids the environment has handed back by then.
/// </summary>
public sealed record GeneratedKpis(
    IReadOnlyList<string> Categories,
    IReadOnlyList<KpiModel> Kpis,
    IReadOnlyList<KpiMeasurementModel> Measurements,
    IReadOnlyList<KpiCheckpointModel> Checkpoints);

/// <summary>One checkpoint of a KPI's plan, named by its KPI's handle.</summary>
public sealed record KpiCheckpointModel(
    string KpiHandle,
    DateOnly Date,
    string Label,
    double PlannedValue,
    double? AtRiskValue);

/// <summary>
/// One KPI on a team or an ART, as the import takes it. <see cref="TeamCode"/> names the record; the category is
/// named and resolved to its id when seeded.
/// </summary>
public sealed record KpiModel(
    string Handle,
    string TeamCode,
    string Name,
    string Description,
    string Category,
    string? Prefix,
    string? Suffix,
    string ExpressedAs,
    string Direction,
    double BaselineValue,
    DateOnly BaselineWindowStart,
    DateOnly BaselineWindowEnd,
    string TargetKind,
    double TargetValue,
    string? AtRiskKind,
    double? AtRiskValue,
    bool IsClosed,
    string WindowKind,
    DateOnly? WindowAnchorDate,
    int? WindowRollingCount,
    string? WindowRollingUnit,
    int? RollingAverageOf);

/// <summary>
/// One value of a KPI, named by its KPI's handle, saying when it was taken the one way its KPI's window takes: a
/// period, an as-of day, or a moment.
/// </summary>
public sealed record KpiMeasurementModel(
    string KpiHandle,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    DateOnly? AsOf,
    DateTimeOffset? MeasuredAt,
    double Value,
    int? SampleSize)
{
    /// <summary>The last day the value covers, whichever way it was taken.</summary>
    public DateOnly LastDay => PeriodEnd ?? AsOf ?? DateOnly.FromDateTime(MeasuredAt!.Value.UtcDateTime);
}
