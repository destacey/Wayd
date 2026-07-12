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
