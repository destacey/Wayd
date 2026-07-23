namespace Wayd.Tools.DataGeneration.Cli.Csv;

// These rows are written as CSV whose headers must match the API import request models
// (Wayd.Web.Api/Models/Organizations/...). CsvHelper maps property names to columns by default.

/// <summary>One row of the employees CSV. Manager is referenced by employee number.</summary>
public sealed class EmployeeCsvRow
{
    public required string EmployeeNumber { get; init; }
    public required string FirstName { get; init; }
    public string? MiddleName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public DateTime? HireDate { get; init; }
    public string? JobTitle { get; init; }
    public string? Department { get; init; }
    public string? OfficeLocation { get; init; }
    public string? ManagerNumber { get; init; }
    public bool IsActive { get; init; } = true;
    public string? EmployeeType { get; init; }
}

/// <summary>One row of the unified teams CSV. <see cref="Type"/> is "Team" or "TeamOfTeams".</summary>
public sealed class TeamCsvRow
{
    public required string Type { get; init; }
    public required string Name { get; init; }
    public required string Code { get; init; }
    public string? Description { get; init; }
    public DateTime ActiveDate { get; init; }
    public bool IsActive { get; init; } = true;
    public DateTime? InactiveDate { get; init; }
}

/// <summary>One row of the staffing CSV: one employee on one team in one role, all by natural key.</summary>
public sealed class TeamMemberCsvRow
{
    public required string TeamCode { get; init; }
    public required string EmployeeNumber { get; init; }
    public required string RoleName { get; init; }
}

/// <summary>One row of the hierarchy CSV: a child team/ToT placed under a parent ToT for a date range.</summary>
public sealed class TeamMembershipCsvRow
{
    public required string ChildCode { get; init; }
    public required string ParentCode { get; init; }
    public required DateTime Start { get; init; }
    public DateTime? End { get; init; }
}

// ---- PPM rows -------------------------------------------------------------------------------------
// Column names must match the API import request models in Wayd.Web.Api/Models/Ppm and
// Wayd.Web.Api/Models/StrategicManagement. Multi-value columns hold semicolon-separated values, matching
// the CsvList helper the import endpoints use.

/// <summary>One row of the strategic themes CSV. Themes are referenced elsewhere by name.</summary>
public sealed class StrategicThemeCsvRow
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string State { get; init; }
}

/// <summary>One row of the portfolios CSV. People are referenced by semicolon-separated employee numbers.</summary>
public sealed class PortfolioCsvRow
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

/// <summary>One row of the programs CSV. Portfolio and themes are referenced by name.</summary>
public sealed class ProgramCsvRow
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

/// <summary>One row of the projects CSV. Key is the project's natural key; everything else is by name.</summary>
public sealed class ProjectCsvRow
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

/// <summary>One row of the project tasks CSV. Project by key; phase and parent task by name.</summary>
public sealed class ProjectTaskCsvRow
{
    public required string ProjectKey { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string PhaseName { get; init; }
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

/// <summary>One row of the project phases CSV: sets one phase's status. Project by key; phase by name.</summary>
public sealed class ProjectPhaseCsvRow
{
    public required string ProjectKey { get; init; }
    public required string PhaseName { get; init; }
    public required string Status { get; init; }
}

/// <summary>One row of the strategic initiatives CSV. Portfolio by name; projects by semicolon-separated keys.</summary>
public sealed class StrategicInitiativeCsvRow
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

/// <summary>One row of the strategic initiative KPIs CSV, attached to its initiative by name.</summary>
public sealed class StrategicInitiativeKpiCsvRow
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

/// <summary>One row of the finalize CSV: closes one program or portfolio after its contents are imported.</summary>
public sealed class PpmFinalizationCsvRow
{
    public required string Type { get; init; }
    public required string Name { get; init; }
    public string? PortfolioName { get; init; }
    public required string Status { get; init; }
    public DateTime? EndDate { get; init; }
}
