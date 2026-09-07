namespace Wayd.Tools.DataGeneration.Cli.Csv;

// The organization CSV rows, as the API import endpoints consume them. Headers must match the request
// models in Wayd.Web.Api/Models/Organizations; CsvHelper maps property names to columns by default.
//
// Organization is the one area whose references are all natural keys the generator already owns — an
// employee number, a team code, a role name — so nothing here needs an id resolved from an earlier run.
// ImportId is carried anyway, so a run's results name the record the way the generator does and a failure
// says which team or person could not be created.

/// <summary>One row of the employees CSV. Manager is referenced by employee number.</summary>
public sealed class EmployeeCsvRow
{
    /// <summary>The employee number, which is already this row's natural key.</summary>
    public required string ImportId { get; init; }

    public required string EmployeeNumber { get; init; }
    public required string FirstName { get; init; }
    public string? MiddleName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }

    /// <summary>
    /// Other work addresses this person is known by. The generator invents one address each, so this is
    /// always empty — but the column has to be here: a missing header fails the whole file.
    /// </summary>
    public string? AdditionalEmails { get; init; }

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
    /// <summary>The team code, which is already this row's natural key.</summary>
    public required string ImportId { get; init; }

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
    /// <summary>Team code and employee number together, since neither identifies a staffing row alone.</summary>
    public required string ImportId { get; init; }

    public required string TeamCode { get; init; }
    public required string EmployeeNumber { get; init; }
    public required string RoleName { get; init; }
}

/// <summary>One row of the hierarchy CSV: a child team/ToT placed under a parent ToT for a date range.</summary>
public sealed class TeamMembershipCsvRow
{
    /// <summary>Child and parent code together, since a child may sit under more than one parent over time.</summary>
    public required string ImportId { get; init; }

    public required string ChildCode { get; init; }
    public required string ParentCode { get; init; }
    public required DateTime Start { get; init; }
    public DateTime? End { get; init; }
}
