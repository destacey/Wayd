namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// The generated KPIs, keyed throughout by the generator's own handles. The seed area projects them to CSV rows,
/// substituting the team ids and category ids the environment has handed back by then.
/// </summary>
public sealed record GeneratedKpis(
    IReadOnlyList<string> Categories,
    IReadOnlyList<KpiModel> Kpis,
    IReadOnlyList<KpiMeasurementModel> Measurements);

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
    bool IsClosed);

/// <summary>One month's value of a KPI, named by its KPI's handle.</summary>
public sealed record KpiMeasurementModel(
    string KpiHandle,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    double Value,
    int? SampleSize);
