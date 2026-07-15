using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Models;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Organization.Domain.Enums;
using Wayd.Organization.Domain.Models;

namespace Wayd.Organization.IntegrationTests.Infrastructure;

/// <summary>
/// Persists Organization entities through their real domain create paths, so every value converter and
/// column type is exercised on the way into the container the same way production writes them.
/// </summary>
internal static class OrganizationSeeder
{
    public static async Task<Team> SeedTeam(WaydDbContext context, string code, string name, CancellationToken cancellationToken)
    {
        var team = Team.Create(
            name,
            new TeamCode(code),
            description: null,
            activeDate: SqlServerDbContextFixture.FixedNow.InUtc().Date,
            Methodology.Kanban,
            SizingMethod.Count,
            SqlServerDbContextFixture.FixedNow);

        await context.Teams.AddAsync(team, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return team;
    }

    public static async Task<Employee> SeedEmployee(WaydDbContext context, string employeeNumber, string email, CancellationToken cancellationToken)
    {
        var employee = Employee.Create(
            new PersonName("Ada", null, "Lovelace"),
            employeeNumber,
            hireDate: SqlServerDbContextFixture.FixedNow,
            new EmailAddress(email),
            jobTitle: "Engineer",
            department: "Engineering",
            officeLocation: null,
            managerId: null,
            isActive: true,
            employeeType: null,
            SqlServerDbContextFixture.FixedNow);

        await context.Employees.AddAsync(employee, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return employee;
    }

    public static async Task<TeamMemberRole> SeedRole(WaydDbContext context, string name, CancellationToken cancellationToken)
    {
        var role = TeamMemberRole.Create(name, description: null).Value;

        await context.TeamMemberRoles.AddAsync(role, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return role;
    }
}
