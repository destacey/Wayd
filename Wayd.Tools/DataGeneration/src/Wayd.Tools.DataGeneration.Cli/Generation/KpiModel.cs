using CsvHelper;
using Wayd.Tools.DataGeneration.Cli.Csv;

namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// The generated KPIs, keyed throughout by the generator's own handles. The seed area projects them to CSV rows,
/// substituting the record ids and category ids the environment has handed back by then.
/// </summary>
public sealed record GeneratedKpis(
    IReadOnlyList<string> Categories,
    IReadOnlyList<KpiModel> Kpis,
    IReadOnlyList<KpiMeasurementModel> Measurements,
    IReadOnlyList<KpiCheckpointModel> Checkpoints);

/// <summary>One checkpoint of a KPI's plan, named by its KPI's handle.</summary>
[CsvRecord]
public sealed partial record KpiCheckpointModel(
    string KpiHandle,
    DateOnly Date,
    string Label,
    double PlannedValue,
    double? AtRiskValue);

/// <summary>The kinds of generated record a KPI can sit on.</summary>
public enum KpiSubject
{
    /// <summary>A team or an ART, named by its team code.</summary>
    Team,

    /// <summary>A project, named by its key.</summary>
    Project,

    /// <summary>A program, named by its name.</summary>
    Program,

    /// <summary>A portfolio, named by its name.</summary>
    Portfolio,

    /// <summary>A strategic initiative, named by its name.</summary>
    StrategicInitiative,
}

/// <summary>
/// One KPI on a team, an ART, a project, a program, a portfolio or a strategic initiative, as the import takes it. <see cref="Subject"/> and
/// <see cref="SubjectHandle"/> name the record by the handle its own generator gave it; the category is named and
/// resolved to its id when seeded.
/// </summary>
[CsvRecord]
public sealed partial record KpiModel(
    string Handle,
    KpiSubject Subject,
    string SubjectHandle,
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
[CsvRecord]
public sealed partial record KpiMeasurementModel(
    string KpiHandle,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    DateOnly? AsOf,
    [CsvFormat(CsvFormats.Instant)] DateTimeOffset? MeasuredAt,
    double Value,
    int? SampleSize)
{
    /// <summary>
    /// The last day the value covers, whichever way it was taken. Stored rather than computed so
    /// <c>kpi-measurements.csv</c> carries it: CsvHelper writes only the members it could read back.
    /// </summary>
    public DateOnly LastDay { get; init; } = PeriodEnd ?? AsOf ?? DateOnly.FromDateTime(MeasuredAt!.Value.UtcDateTime);
}
