using System.Globalization;

namespace Wayd.Tools.DataGeneration.Cli.Csv;

// The Planning CSV rows, as the API import endpoints consume them. Column names must match the request models
// in Wayd.Web.Api/Models/Planning, and every column has to be present even when the seed leaves it empty: a
// missing header fails the whole file. Statuses, categories and grades are numeric ids.

/// <summary>One row of the planning intervals CSV, with the roster on the row.</summary>
public sealed class PlanningIntervalCsvRow
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
}

/// <summary>One row of the objectives CSV. Each row names its own planning interval.</summary>
public sealed class PlanningIntervalObjectiveCsvRow
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
}

/// <summary>One row of the risks CSV. People are employee ids.</summary>
public sealed class RiskCsvRow
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
}

public static class PlanningCsv
{
    /// <summary>
    /// A UTC instant for the Planning imports' <c>*Utc</c> columns, written without a zone designator.
    /// </summary>
    /// <remarks>
    /// Those columns bind to a <c>DateTime</c> that the endpoint then labels UTC. CsvHelper parses a value
    /// ending in <c>Z</c> into the server's local time first, so the designator would move every instant by
    /// the server's offset — and a report dated late today could land in the future and be refused.
    /// </remarks>
    public static string Timestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
}
