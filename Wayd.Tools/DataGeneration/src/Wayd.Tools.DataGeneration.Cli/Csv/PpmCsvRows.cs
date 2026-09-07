namespace Wayd.Tools.DataGeneration.Cli.Csv;

// The PPM CSV rows, as the API import endpoints consume them. Column names must match the request models
// in Wayd.Web.Api/Models/Ppm and Wayd.Web.Api/Models/StrategicManagement; multi-value columns hold
// semicolon-separated values, matching the CsvList helper those endpoints use.
//
// Every row carries an ImportId, and the seed always sets it to the generator's own handle for that record
// — a portfolio's name, a project's key. That is what makes a run's results directly usable: the ids come
// back keyed by the same handle the next file references, with no second mapping to keep in step. Rows
// that point at another record do so by id, because none of the names involved is uniquely indexed.

/// <summary>One row of the strategic themes CSV.</summary>
public sealed class StrategicThemeCsvRow
{
    public required string ImportId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string State { get; init; }
}

/// <summary>
/// One row of the portfolios CSV. People are referenced by semicolon-separated employee numbers. A
/// portfolio has no planned timeline of its own, so it carries only the transition dates — and no closing
/// one, since an import cannot close a portfolio.
/// </summary>
public sealed class PortfolioCsvRow
{
    public required string ImportId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Status { get; init; }
    public required DateTime CreatedOn { get; init; }
    public DateTime? ActivatedOn { get; init; }
    public string? Sponsors { get; init; }
    public string? Owners { get; init; }
    public string? Managers { get; init; }
}

/// <summary>One row of the programs CSV.</summary>
public sealed class ProgramCsvRow
{
    public required string ImportId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required Guid PortfolioId { get; init; }
    public required string Status { get; init; }

    /// <summary>The timeline the program plans to run over, which its transitions read but never set.</summary>
    public DateTime? Start { get; init; }
    public DateTime? End { get; init; }

    /// <summary>When the program actually moved. No closing date: an import cannot close a program.</summary>
    public required DateTime CreatedOn { get; init; }
    public DateTime? ActivatedOn { get; init; }

    /// <summary>Semicolon-separated strategic theme ids.</summary>
    public string? StrategicThemes { get; init; }

    public string? Sponsors { get; init; }
    public string? Owners { get; init; }
    public string? Managers { get; init; }
}

/// <summary>One row of the projects CSV. Key is the project's natural key, which tasks and initiatives use.</summary>
public sealed class ProjectCsvRow
{
    public required string ImportId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Key { get; init; }
    public required Guid PortfolioId { get; init; }
    public required int ExpenditureCategoryId { get; init; }
    public required string Status { get; init; }
    public Guid? ProgramId { get; init; }
    public Guid? ProjectLifecycleId { get; init; }
    public string? BusinessCase { get; init; }
    public string? ExpectedBenefits { get; init; }
    /// <summary>The timeline the project plans to run over, which is not the same as when it moved.</summary>
    public DateTime? Start { get; init; }
    public DateTime? End { get; init; }

    /// <summary>
    /// When the project actually moved. Each replayed transition is stamped with the matching one, so these
    /// are what the project's status history ends up dated by.
    /// </summary>
    public required DateTime CreatedOn { get; init; }
    public DateTime? ActivatedOn { get; init; }
    public DateTime? ClosedOn { get; init; }

    /// <summary>Semicolon-separated strategic theme ids.</summary>
    public string? StrategicThemes { get; init; }

    public string? Sponsors { get; init; }
    public string? Owners { get; init; }
    public string? Managers { get; init; }
    public string? Members { get; init; }
}

/// <summary>
/// One row of the project tasks CSV. The project is named by key; the stage by name, which is unique
/// within a project because stages are copied from its lifecycle.
/// </summary>
public sealed class ProjectTaskCsvRow
{
    public required string ImportId { get; init; }
    public required string ProjectKey { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string StageName { get; init; }

    /// <summary>The ImportId of the task row this one nests under, where it has a parent.</summary>
    public string? ParentImportId { get; init; }

    /// <summary>
    /// An existing task to nest under. A seed builds a whole breakdown in one file, so every parent it
    /// names is in that file and this stays empty — but the column has to be here: a missing header fails
    /// the whole file.
    /// </summary>
    public Guid? ParentTaskId { get; init; }

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

/// <summary>One row of the project stages CSV: sets one stage's status.</summary>
public sealed class ProjectStageCsvRow
{
    public required string ImportId { get; init; }
    public required string ProjectKey { get; init; }
    public required string StageName { get; init; }
    public required string Status { get; init; }
}

/// <summary>One row of the strategic initiatives CSV. Projects are referenced by semicolon-separated keys.</summary>
public sealed class StrategicInitiativeCsvRow
{
    public required string ImportId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required Guid PortfolioId { get; init; }
    public required string Status { get; init; }
    public DateTime Start { get; init; }
    public DateTime End { get; init; }
    public string? ProjectKeys { get; init; }
    public string? Sponsors { get; init; }
    public string? Owners { get; init; }
}

/// <summary>One row of the strategic initiative KPIs CSV, attached to its initiative row by that row's ImportId.</summary>
public sealed class StrategicInitiativeKpiCsvRow
{
    public required string StrategicInitiativeImportId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public double TargetValue { get; init; }
    public double? StartingValue { get; init; }
    public string? Prefix { get; init; }
    public string? Suffix { get; init; }
    public required string TargetDirection { get; init; }
}

/// <summary>
/// One row of the finalize CSV: closes one program or portfolio once its contents are imported. Id is read
/// per Type — a program id on a program row, a portfolio id on a portfolio row.
/// </summary>
public sealed class PpmFinalizationCsvRow
{
    public required string ImportId { get; init; }
    public required string Type { get; init; }
    public required Guid Id { get; init; }
    public required string Status { get; init; }
    public DateTime? EndDate { get; init; }
}
