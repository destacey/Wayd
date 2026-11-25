using CsvHelper;
using System.Globalization;

namespace Wayd.Tools.DataGeneration.Cli.Csv;

// The Planning CSV rows, as the API import endpoints consume them. Column names must match the request models
// in Wayd.Web.Api/Models/Planning, and every column has to be present even when the seed leaves it empty: a
// missing header fails the whole file. Statuses, categories and grades are numeric ids.

/// <summary>One row of the planning intervals CSV, with the roster on the row.</summary>
[CsvRecord]
public sealed partial class PlanningIntervalCsvRow
{
    public required string ImportId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required DateOnly Start { get; init; }
    public required DateOnly End { get; init; }
    public required int IterationWeeks { get; init; }
    public string? IterationPrefix { get; init; }

    /// <summary>Semicolon-separated team ids.</summary>
    public string? TeamIds { get; init; }

    /// <summary>The record's links, <c>Name|URL</c> entries separated by semicolons. See <see cref="Generation.SeedLinks"/>.</summary>
    public string? Links { get; init; }
}

/// <summary>One row of the objectives CSV. Each row names its own planning interval.</summary>
[CsvRecord]
public sealed partial class PlanningIntervalObjectiveCsvRow
{
    public required string ImportId { get; init; }
    public required Guid PlanningIntervalId { get; init; }
    public required Guid TeamId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required int StatusId { get; init; }
    public required double Progress { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? TargetDate { get; init; }
    public required bool IsStretch { get; init; }
    public string? ClosedDateUtc { get; init; }
    public int? Order { get; init; }

    /// <summary>The record's links, <c>Name|URL</c> entries separated by semicolons. See <see cref="Generation.SeedLinks"/>.</summary>
    public string? Links { get; init; }
}

/// <summary>One row of the risks CSV. People are employee ids.</summary>
[CsvRecord]
public sealed partial class RiskCsvRow
{
    public required string ImportId { get; init; }
    public required Guid TeamId { get; init; }
    public required string Summary { get; init; }
    public string? Description { get; init; }
    public required string ReportedOnUtc { get; init; }
    public required Guid ReportedById { get; init; }
    public required int StatusId { get; init; }
    public required int CategoryId { get; init; }
    public required int ImpactId { get; init; }
    public required int LikelihoodId { get; init; }
    public Guid? AssigneeId { get; init; }
    public DateOnly? FollowUpDate { get; init; }
    public string? Response { get; init; }
    public string? ClosedDateUtc { get; init; }

    /// <summary>The record's links, <c>Name|URL</c> entries separated by semicolons. See <see cref="Generation.SeedLinks"/>.</summary>
    public string? Links { get; init; }
}

public static class PlanningCsv
{
    /// <summary>
    /// A UTC instant for the Planning imports' <c>*Utc</c> columns, written without a zone designator.
    /// </summary>
    /// <remarks>
    /// Those columns bind to a <c>DateTime</c> that the endpoint then labels UTC, so a bare timestamp is what
    /// they document. Converting first keeps an instant written with another offset from being read as that
    /// wall-clock time in UTC.
    /// </remarks>
    public static string Timestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
}
