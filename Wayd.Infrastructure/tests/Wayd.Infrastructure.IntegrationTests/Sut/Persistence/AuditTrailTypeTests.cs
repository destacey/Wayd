using Microsoft.EntityFrameworkCore;
using Wayd.Infrastructure.Auditing;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Models;
using Wayd.Common.Domain.Events;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves an audit row says which kind of change it records, including for an entity that has nothing but
/// key columns.
/// </summary>
/// <remarks>
/// The trail type used to be assigned inside the loop over an entry's properties, and that loop skips
/// primary keys, complex types and the system audit columns before it reaches the assignment. A join entity
/// whose every property is part of its composite key therefore never reached it and was written as
/// <c>Type="None"</c> — an insert that claimed nothing happened. <c>RoleAssignment</c> is the case that
/// surfaced it, and at 88,455 rows in one seed it was the largest single contributor to the table.
/// <para>
/// A real provider is what shows it: the type is decided while EF walks the entry's properties, and which
/// properties an entry reports depends on the model's key and column mapping. The in-memory provider maps
/// these differently, so it reports a type either way.
/// </para>
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class AuditTrailTypeTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static readonly Instant CreatedAt = Instant.FromUtc(2026, 5, 1, 9, 0, 0);

    [Fact]
    public async Task SaveChanges_EntityWithOnlyKeyColumns_RecordsItAsACreate()
    {
        // Arrange
        await using var context = _fixture.CreateContext();

        var owner = (await SeedEmployee(context)).Id;
        var portfolio = ProjectPortfolio.Create(
            $"Audit type {Guid.NewGuid():N}",
            "Proves a key-only join entity records its insert.",
            new Dictionary<ProjectPortfolioRole, HashSet<Guid>> { [ProjectPortfolioRole.Owner] = [owner] },
            EventActor.System,
            CreatedAt);

        context.Set<ProjectPortfolio>().Add(portfolio);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var roleTrails = await context.AuditTrails
            .AsNoTracking()
            .Where(t => t.TableName == "RoleAssignment`1" && t.PrimaryKey!.Contains(portfolio.Id.ToString()))
            .ToListAsync(TestContext.Current.CancellationToken);

        roleTrails.Should().NotBeEmpty("the role assignment is audited, so its insert has a trail row");
        roleTrails.Should().OnlyContain(t => t.Type == nameof(TrailType.Create));
    }

    [Fact]
    public async Task SaveChanges_EntityWithOrdinaryColumns_StillRecordsItAsACreate()
    {
        // Arrange — the aggregate itself has non-key columns, so it reached the assignment either way.
        // Kept so a change that settles the type from state cannot regress the case that already worked.
        await using var context = _fixture.CreateContext();

        var portfolio = ProjectPortfolio.Create(
            $"Audit type {Guid.NewGuid():N}",
            "Proves an ordinary insert still records as a create.",
            roles: null,
            EventActor.System,
            CreatedAt);

        context.Set<ProjectPortfolio>().Add(portfolio);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var trail = await context.AuditTrails
            .AsNoTracking()
            .Where(t => t.TableName == nameof(ProjectPortfolio) && t.PrimaryKey!.Contains(portfolio.Id.ToString()))
            .ToListAsync(TestContext.Current.CancellationToken);

        trail.Should().ContainSingle().Which.Type.Should().Be(nameof(TrailType.Create));
    }

    /// <summary>A real employee, because the role assignment has a foreign key to one.</summary>
    private static async Task<Employee> SeedEmployee(WaydDbContext context)
    {
        var employee = Employee.Create(
            new PersonName("Audit", null, "Probe"),
            $"{Guid.CreateVersion7():N}"[..12],
            CreatedAt,
            new EmailAddress($"{Guid.CreateVersion7():N}"[..12] + "@acme.example"),
            jobTitle: null,
            department: null,
            officeLocation: null,
            managerId: null,
            isActive: true,
            employeeType: null,
            CreatedAt);

        context.Employees.Add(employee);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return employee;
    }
}
