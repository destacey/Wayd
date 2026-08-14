using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="GetProjectStatusHistoryQueryHandler"/> against a real SQL Server
/// container.
/// <para>
/// These need the container. The handler materialises entities and maps them in memory, so the acting
/// employee only appears if the query loads that navigation explicitly. Against the in-memory
/// <c>FakeWaydDbContext</c>, <c>.Include</c> is a no-op and the faker sets the navigation by hand, so the
/// unit test passes whether or not the query loads it — which is exactly how a query that returned no
/// employee at all reached production.
/// </para>
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetProjectStatusHistoryQueryHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public GetProjectStatusHistoryQueryHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Seeds a project through the real domain create path and walks it to Active, so the history rows are
    /// written exactly as production writes them — converters, varchar status columns and all.
    /// </summary>
    private async Task<(Guid ProjectId, Employee Actor)> SeedProjectWithHistory(CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateContext();

        var employee = Employee.Create(
            new PersonName("Ada", null, "Lovelace"),
            "E1000",
            hireDate: SqlServerDbContextFixture.FixedNow,
            new EmailAddress("ada.lovelace@acme.example"),
            jobTitle: "Delivery Lead",
            department: "Engineering",
            officeLocation: null,
            managerId: null,
            isActive: true,
            employeeType: null,
            SqlServerDbContextFixture.FixedNow);
        await context.Employees.AddAsync(employee, cancellationToken);

        var category = ExpenditureCategory.Create("Capital", "Capital spend", isCapitalizable: true, requiresDepreciation: true);
        await context.ExpenditureCategories.AddAsync(category, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var portfolio = ProjectPortfolio.Create("Delivery", "Delivery portfolio");
        await context.Portfolios.AddAsync(portfolio, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var actor = new PpmActor(employee.Id, IsPpmAdministrator: true, "integration-test-user");

        portfolio.Activate(actor, SqlServerDbContextFixture.FixedNow.InUtc().Date);

        var today = SqlServerDbContextFixture.FixedNow.InUtc().Date;
        var createResult = portfolio.CreateProject(
            "Apollo",
            "Apollo description",
            new ProjectKey("APOLLO"),
            category.Id,
            new LocalDateRange(today, today.PlusMonths(3)),
            programId: null,
            businessCase: null,
            expectedBenefits: null,
            roles: null,
            strategicThemes: null,
            SqlServerDbContextFixture.FixedNow,
            actor);

        var project = createResult.Value;

        // A real transition, attributed to the seeded employee, so the row carries a resolvable link.
        project.Activate(actor, ProjectAncestryRoles.None, SqlServerDbContextFixture.FixedNow);

        await context.SaveChangesAsync(cancellationToken);

        return (project.Id, employee);
    }

    [Fact]
    public async Task Handle_ResolvesTheActingEmployee_FromTheDatabase()
    {
        // Arrange
        await _fixture.ResetPpmData(TestContext.Current.CancellationToken);
        var (projectId, employee) = await SeedProjectWithHistory(TestContext.Current.CancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = new GetProjectStatusHistoryQueryHandler(context);

        // Act
        var result = await handler.Handle(
            new GetProjectStatusHistoryQuery(projectId),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(entry =>
        {
            entry.ChangedBy.Should().NotBeNull("the acting employee must be loaded, not left null by a missing Include");
            entry.ChangedBy!.Name.Should().Be(employee.Name.DisplayName);
        });
    }

    [Fact]
    public async Task Handle_ReturnsTheFullChain_NewestFirst()
    {
        // Arrange
        await _fixture.ResetPpmData(TestContext.Current.CancellationToken);
        var (projectId, _) = await SeedProjectWithHistory(TestContext.Current.CancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = new GetProjectStatusHistoryQueryHandler(context);

        // Act
        var result = await handler.Handle(
            new GetProjectStatusHistoryQuery(projectId),
            TestContext.Current.CancellationToken);

        // Assert
        // Creation and activation share one timestamp here, exactly as an import writes them, so the order
        // can only come from following the chain.
        result.Select(h => h.ToStatus.Name).Should().Equal(
            nameof(ProjectStatus.Active),
            nameof(ProjectStatus.Proposed));
        result.Last().FromStatus.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PersistsAndReadsBackTheRecordedSource()
    {
        // Arrange
        await _fixture.ResetPpmData(TestContext.Current.CancellationToken);
        var (projectId, _) = await SeedProjectWithHistory(TestContext.Current.CancellationToken);

        await using var context = _fixture.CreateContext();

        // Act
        var stored = await context.Set<ProjectStatusHistory>()
            .AsNoTracking()
            .Where(h => h.ProjectId == projectId)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        // The varchar/PascalCase round trip only happens under the real provider.
        stored.Should().AllSatisfy(h => h.Source.Should().Be(ProjectStatusHistorySource.Recorded));
        stored.Should().Contain(h => h.ToStatus == ProjectStatus.Active);
    }
}
