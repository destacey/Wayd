namespace Wayd.Tools.DataGeneration.Cli.Generation;

// The generated Planning model, keyed by the generator's own handles — interval names, team codes, employee
// numbers — for the same reason as the other models: ids exist only once the API has created something, so
// each seed area substitutes them as it writes its file.

/// <summary>
/// A generated planning interval, run by the ART named by <see cref="ArtCode"/>. <see cref="TeamCodes"/> is
/// its roster: the ART itself and each of its teams, semicolon-separated.
/// </summary>
public sealed class PlanningIntervalModel
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required DateOnly Start { get; init; }
    public required DateOnly End { get; init; }
    public required int IterationWeeks { get; init; }
    public required string IterationPrefix { get; init; }
    public required string ArtCode { get; init; }
    public required string TeamCodes { get; init; }
}

/// <summary>
/// A generated objective of the team named by <see cref="TeamCode"/> on the interval named by
/// <see cref="PlanningIntervalName"/>. A closed status carries <see cref="ClosedAt"/>; any other has none.
/// </summary>
public sealed class PlanningIntervalObjectiveModel
{
    public required string PlanningIntervalName { get; init; }
    public required string TeamCode { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Status { get; init; }
    public required double Progress { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly TargetDate { get; init; }
    public required bool IsStretch { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public required int Order { get; init; }

    /// <summary>Unique per row: the order is unique within one team's objectives on one interval.</summary>
    public string ImportId => $"{PlanningIntervalName}|{TeamCode}|{Order}";
}

/// <summary>
/// A generated risk raised by the team named by <see cref="TeamCode"/>. People are employee numbers. A
/// closed risk carries <see cref="ClosedAt"/>, and only an open one is followed up.
/// </summary>
public sealed class RiskModel
{
    public required string Handle { get; init; }
    public required string TeamCode { get; init; }
    public required string Summary { get; init; }
    public required string Description { get; init; }
    public required DateTimeOffset ReportedAt { get; init; }
    public required string ReportedByEmployeeNumber { get; init; }
    public required string Status { get; init; }
    public required string Category { get; init; }
    public required string Impact { get; init; }
    public required string Likelihood { get; init; }
    public string? AssigneeEmployeeNumber { get; init; }
    public DateOnly? FollowUpDate { get; init; }
    public string? Response { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
}
