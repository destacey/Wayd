namespace Wayd.Tools.DataGeneration.Cli.Csv;

// The KPI CSV rows, as the API import endpoint consumes them. Column names must match ImportKpiRequest and
// ImportKpiMeasurementRequest in Wayd.Web.Api/Models/Kpis, and every column has to be present even when the
// seed leaves it empty: a missing header fails the whole file.

/// <summary>One row of the KPI file: a KPI on a team or a team of teams, by id.</summary>
public sealed class KpiCsvRow
{
    public required string ImportId { get; init; }
    public required string SubjectType { get; init; }
    public required Guid SubjectId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public Guid? CategoryId { get; init; }
    public string? Prefix { get; init; }
    public string? Suffix { get; init; }
    public string? ExpressedAs { get; init; }
    public required string Direction { get; init; }
    public double? BaselineValue { get; init; }
    public DateOnly? BaselineWindowStart { get; init; }
    public DateOnly? BaselineWindowEnd { get; init; }
    public string? TargetKind { get; init; }
    public required double TargetValue { get; init; }
    public string? AtRiskKind { get; init; }
    public double? AtRiskValue { get; init; }
    public bool? IsClosed { get; init; }
}

/// <summary>One row of the measurements file: one month of one KPI, named by the KPI row's ImportId.</summary>
public sealed class KpiMeasurementCsvRow
{
    public required string KpiImportId { get; init; }
    public required DateOnly PeriodStart { get; init; }
    public required DateOnly PeriodEnd { get; init; }
    public required double Value { get; init; }
    public DateOnly? MeasuredOn { get; init; }
    public int? SampleSize { get; init; }
    public string? Note { get; init; }
}
