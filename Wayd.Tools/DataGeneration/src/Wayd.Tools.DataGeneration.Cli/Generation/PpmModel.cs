namespace Wayd.Tools.DataGeneration.Cli.Generation;

// The generated PPM model, keyed by the generator's own handles — portfolio and program names, project
// keys — because those are what the generator can decide for itself, and what its own cross-references are
// built from. Ids exist only once the API has created something, so they appear where a model row is
// projected to a CSV row and nowhere earlier. Each seed area owns that projection.

/// <summary>A generated strategic theme. Programs and projects reference it by name here; the areas that post them resolve those names to ids.</summary>
public sealed class StrategicThemeModel
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string State { get; init; }
}

/// <summary>A generated portfolio. People are referenced by employee number, which stays a natural key end to end.</summary>
public sealed class PortfolioModel
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Status { get; init; }
    public DateTime? Start { get; init; }
    public DateTime? End { get; init; }
    public string? Sponsors { get; init; }
    public string? Owners { get; init; }
    public string? Managers { get; init; }
}

/// <summary>A generated program, under the portfolio named by <see cref="PortfolioName"/>.</summary>
public sealed class ProgramModel
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string PortfolioName { get; init; }
    public required string Status { get; init; }
    public DateTime? Start { get; init; }
    public DateTime? End { get; init; }
    public string? StrategicThemes { get; init; }
    public string? Sponsors { get; init; }
    public string? Owners { get; init; }
    public string? Managers { get; init; }
}

/// <summary>A generated project. <see cref="Key"/> is its natural key, which tasks and initiatives reference; everything it points at is named here and resolved to an id by the project area.</summary>
public sealed class ProjectModel
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Key { get; init; }
    public required string PortfolioName { get; init; }
    public required string ExpenditureCategoryName { get; init; }
    public required string Status { get; init; }
    public string? ProgramName { get; init; }
    public string? ProjectLifecycleName { get; init; }
    public string? BusinessCase { get; init; }
    public string? ExpectedBenefits { get; init; }
    public DateTime? Start { get; init; }
    public DateTime? End { get; init; }
    public string? StrategicThemes { get; init; }
    public string? Sponsors { get; init; }
    public string? Owners { get; init; }
    public string? Managers { get; init; }
    public string? Members { get; init; }
}

/// <summary>A generated project task, under the project named by <see cref="ProjectKey"/> and nested under <see cref="ParentTaskName"/> where it has one.</summary>
public sealed class ProjectTaskModel
{
    public required string ProjectKey { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string StageName { get; init; }
    public string? ParentTaskName { get; init; }
    public required string Type { get; init; }
    public required string Status { get; init; }
    public required string Priority { get; init; }
    public decimal? Progress { get; init; }
    public DateTime? PlannedStart { get; init; }
    public DateTime? PlannedEnd { get; init; }
    public DateTime? PlannedDate { get; init; }
    public decimal? EstimatedEffortHours { get; init; }
    public string? Assignees { get; init; }
}

/// <summary>A generated stage status: one stage of one project, set to exactly the status the generator computed from that stage’s tasks.</summary>
public sealed class ProjectStageModel
{
    public required string ProjectKey { get; init; }
    public required string StageName { get; init; }
    public required string Status { get; init; }
}

/// <summary>A generated strategic initiative, under the portfolio named by <see cref="PortfolioName"/>, delivering through the projects named by <see cref="ProjectKeys"/>.</summary>
public sealed class StrategicInitiativeModel
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string PortfolioName { get; init; }
    public required string Status { get; init; }
    public DateTime Start { get; init; }
    public DateTime End { get; init; }
    public string? ProjectKeys { get; init; }
    public string? Sponsors { get; init; }
    public string? Owners { get; init; }
}

/// <summary>A generated KPI, attached to the initiative named by <see cref="StrategicInitiativeName"/>.</summary>
public sealed class StrategicInitiativeKpiModel
{
    public required string StrategicInitiativeName { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public double TargetValue { get; init; }
    public double? StartingValue { get; init; }
    public string? Prefix { get; init; }
    public string? Suffix { get; init; }
    public required string TargetDirection { get; init; }
}

/// <summary>A generated finalization: closes one program or portfolio once its contents exist.</summary>
public sealed class PpmFinalizationModel
{
    public required string Type { get; init; }
    public required string Name { get; init; }
    public string? PortfolioName { get; init; }
    public required string Status { get; init; }
    public DateTime? EndDate { get; init; }
}
