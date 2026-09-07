using Wayd.Tools.DataGeneration.Cli.Csv;

namespace Wayd.Tools.DataGeneration.Cli.Seeding.Areas;

/// <summary>The stable names the organization areas are known by, so dependencies read as references.</summary>
public static class OrganizationArea
{
    public const string Employees = "organization.employees";
    public const string Teams = "organization.teams";
    public const string TeamHierarchy = "organization.team-hierarchy";
    public const string Roles = "organization.roles";
    public const string Staffing = "organization.staffing";
}

/// <summary>
/// Loads the people. Everything else in a seed hangs off an employee number, so this runs first — but it
/// depends on nothing, because an employee's own manager is named within the same file.
/// </summary>
public sealed class EmployeesArea() : SeedArea(OrganizationArea.Employees)
{
    public override bool ShouldRun(SeedContext context) => context.Org.Employees.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        context.Log($"Importing {context.Org.Employees.Count} employees...");

        var run = await context.Client.ImportEmployees(CsvFile.ToBytes(context.Org.Employees), cancellationToken);
        context.Publish(Name, run.CreatedIdsByImportId);
    }
}

/// <summary>
/// Loads the teams and teams of teams. Creating a team replicates it into the PPM, Planning and Work
/// projections, which is why nothing that references a team can run until this has finished.
/// </summary>
public sealed class TeamsArea() : SeedArea(OrganizationArea.Teams)
{
    public override bool ShouldRun(SeedContext context) => context.Org.Teams.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        context.Log($"Importing {context.Org.Teams.Count} teams...");

        var run = await context.Client.ImportTeams(CsvFile.ToBytes(context.Org.Teams), cancellationToken);
        context.Publish(Name, run.CreatedIdsByImportId);
    }
}

/// <summary>Places teams under one another. Both ends of every link have to exist first.</summary>
public sealed class TeamHierarchyArea() : SeedArea(OrganizationArea.TeamHierarchy, OrganizationArea.Teams)
{
    public override bool ShouldRun(SeedContext context) => context.Org.TeamMemberships.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        context.Log($"Importing {context.Org.TeamMemberships.Count} team-hierarchy memberships...");

        var run = await context.Client.ImportTeamMemberships(CsvFile.ToBytes(context.Org.TeamMemberships), cancellationToken);
        context.Publish(Name, run.CreatedIdsByImportId);
    }
}

/// <summary>
/// Creates the team member roles the staffing file names. Roles are settings rather than an import, and
/// the staffing import creates none of them — it resolves each row's role and rejects what it cannot find.
/// </summary>
public sealed class RolesArea() : SeedArea(OrganizationArea.Roles)
{
    public override bool ShouldRun(SeedContext context) => context.Org.RoleNames.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        context.Log($"Ensuring {context.Org.RoleNames.Count} team member roles exist...");

        await context.Client.EnsureRoles(context.Org.RoleNames, cancellationToken);
    }
}

/// <summary>Puts people on teams. Needs the people, the teams, and the roles it names.</summary>
public sealed class StaffingArea() : SeedArea(
    OrganizationArea.Staffing, OrganizationArea.Employees, OrganizationArea.Teams, OrganizationArea.Roles)
{
    public override bool ShouldRun(SeedContext context) => context.Org.Members.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        context.Log($"Importing {context.Org.Members.Count} staffing rows...");

        var run = await context.Client.ImportTeamMembers(CsvFile.ToBytes(context.Org.Members), cancellationToken);
        context.Publish(Name, run.CreatedIdsByImportId);
    }
}
