using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Application.Projects.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="GetProjectQueryHandler"/> against a real SQL Server container.
/// <para>
/// These need the container. The handler builds its DTO with <c>ProjectToType</c>, so every mapping clause
/// must translate to SQL, and one that cannot throws only under a real provider — the in-memory
/// <c>FakeWaydDbContext</c> runs the same projection as LINQ-to-Objects and evaluates anything. Any change
/// to the <c>ProjectDetailsDto</c> mapping needs a test here, not just a unit test.
/// </para>
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetProjectQueryHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public GetProjectQueryHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
        MapsterConfiguration.Ensure();
    }

    /// <summary>
    /// Seeds a project through the real domain create path and walks it to the requested status, so the
    /// row is written exactly as production writes it.
    /// </summary>
    private async Task<(Guid ProjectId, string Key, Guid EmployeeId)> SeedProject(
        ProjectStatus targetStatus,
        CancellationToken cancellationToken,
        bool withLifecycle = true,
        bool withDates = true)
    {
        await using var context = _fixture.CreateContext();

        var employee = Employee.Create(
            new PersonName("Grace", null, "Hopper"),
            "E2000",
            hireDate: SqlServerDbContextFixture.FixedNow,
            new EmailAddress("grace.hopper@acme.example"),
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
            "Gemini",
            "Gemini description",
            new ProjectKey("GEMINI"),
            category.Id,
            withDates ? new LocalDateRange(today, today.PlusMonths(3)) : null,
            programId: null,
            businessCase: null,
            expectedBenefits: null,
            roles: null,
            strategicThemes: null,
            SqlServerDbContextFixture.FixedNow,
            actor);

        var project = createResult.Value;

        if (withLifecycle)
        {
            var lifecycle = ProjectLifecycle.Create(
                "Standard",
                "Standard delivery lifecycle",
                [("Delivery", "Delivery stage")]);
            lifecycle.Activate();
            await context.Set<ProjectLifecycle>().AddAsync(lifecycle, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            project.AssignLifecycle(actor, ProjectAncestryRoles.None, lifecycle);
        }

        if (targetStatus is ProjectStatus.Canceled)
        {
            project.Cancel(actor, ProjectAncestryRoles.None, SqlServerDbContextFixture.FixedNow);
        }

        if (targetStatus is ProjectStatus.Active or ProjectStatus.Completed)
        {
            project.Activate(actor, ProjectAncestryRoles.None, SqlServerDbContextFixture.FixedNow);
        }

        if (targetStatus is ProjectStatus.Completed)
        {
            project.Complete(actor, ProjectAncestryRoles.None, SqlServerDbContextFixture.FixedNow);
        }

        await context.SaveChangesAsync(cancellationToken);

        return (project.Id, project.Key.Value, employee.Id);
    }

    [Fact]
    public async Task Handle_ProjectsTheProject_WithoutAnUntranslatableClause()
    {
        // Arrange
        await _fixture.ResetPpmData(TestContext.Current.CancellationToken);
        var (_, key, employeeId) = await SeedProject(ProjectStatus.Completed, TestContext.Current.CancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = new GetProjectQueryHandler(context, FixedDateTimeProvider(), CurrentPrincipal(employeeId));

        // Act — the projection runs in SQL here, so a mapping clause that cannot be translated throws
        // rather than being silently evaluated in memory.
        var dto = await handler.Handle(
            new GetProjectQuery(new ProjectIdOrKey(key)),
            TestContext.Current.CancellationToken);

        // Assert
        dto.Should().NotBeNull();
        dto!.Key.Should().Be(key);
        dto.Status.Name.Should().Be(nameof(ProjectStatus.Completed));
    }

    [Fact]
    public async Task Handle_ReturnsTheBackwardStatusTargets_ForAClosedProject()
    {
        // Arrange
        await _fixture.ResetPpmData(TestContext.Current.CancellationToken);
        var (_, key, employeeId) = await SeedProject(ProjectStatus.Completed, TestContext.Current.CancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = new GetProjectQueryHandler(context, FixedDateTimeProvider(), CurrentPrincipal(employeeId));

        // Act
        var dto = await handler.Handle(
            new GetProjectQuery(new ProjectIdOrKey(key)),
            TestContext.Current.CancellationToken);

        // Assert — the UI gates the Revert Status action on this list, so it has to survive the round trip
        // rather than being dropped by the projection.
        dto.Should().NotBeNull();
        dto!.BackwardStatusTargets.Select(t => t.Name).Should().Equal(
            nameof(ProjectStatus.Proposed),
            nameof(ProjectStatus.Approved),
            nameof(ProjectStatus.Active));
    }

    [Fact]
    public async Task Handle_ReturnsNoBackwardStatusTargets_ForAProposedProject()
    {
        // Arrange
        await _fixture.ResetPpmData(TestContext.Current.CancellationToken);
        var (_, key, employeeId) = await SeedProject(ProjectStatus.Proposed, TestContext.Current.CancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = new GetProjectQueryHandler(context, FixedDateTimeProvider(), CurrentPrincipal(employeeId));

        // Act
        var dto = await handler.Handle(
            new GetProjectQuery(new ProjectIdOrKey(key)),
            TestContext.Current.CancellationToken);

        // Assert — a proposed project is already at the start of its lifecycle.
        dto.Should().NotBeNull();
        dto!.BackwardStatusTargets.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OffersOnlyProposed_ForAProjectCancelledWithNoLifecycleOrDates()
    {
        // Arrange — cancelling straight from Proposed is legal, so this project never had a lifecycle or a
        // timeline. Approved needs the lifecycle and Active needs the dates, so neither can be offered.
        await _fixture.ResetPpmData(TestContext.Current.CancellationToken);
        var (_, key, employeeId) = await SeedProject(
            ProjectStatus.Canceled,
            TestContext.Current.CancellationToken,
            withLifecycle: false,
            withDates: false);

        await using var context = _fixture.CreateContext();
        var handler = new GetProjectQueryHandler(context, FixedDateTimeProvider(), CurrentPrincipal(employeeId));

        // Act
        var dto = await handler.Handle(
            new GetProjectQuery(new ProjectIdOrKey(key)),
            TestContext.Current.CancellationToken);

        // Assert
        dto.Should().NotBeNull();
        dto!.BackwardStatusTargets.Select(t => t.Name).Should().Equal(nameof(ProjectStatus.Proposed));
    }

    [Fact]
    public async Task Handle_OffersActiveButNotApproved_ForACancelledProjectWithDatesButNoLifecycle()
    {
        // Arrange — activating does not require a lifecycle, but approving does, so the two gates diverge.
        await _fixture.ResetPpmData(TestContext.Current.CancellationToken);
        var (_, key, employeeId) = await SeedProject(
            ProjectStatus.Canceled,
            TestContext.Current.CancellationToken,
            withLifecycle: false,
            withDates: true);

        await using var context = _fixture.CreateContext();
        var handler = new GetProjectQueryHandler(context, FixedDateTimeProvider(), CurrentPrincipal(employeeId));

        // Act
        var dto = await handler.Handle(
            new GetProjectQuery(new ProjectIdOrKey(key)),
            TestContext.Current.CancellationToken);

        // Assert
        dto.Should().NotBeNull();
        dto!.BackwardStatusTargets.Select(t => t.Name).Should().Equal(
            nameof(ProjectStatus.Proposed),
            nameof(ProjectStatus.Active));
    }

    private static IDateTimeProvider FixedDateTimeProvider()
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(SqlServerDbContextFixture.FixedNow);
        dateTimeProvider.SetupGet(d => d.Today).Returns(SqlServerDbContextFixture.FixedNow.InUtc().Date);

        return dateTimeProvider.Object;
    }

    private static ICurrentPrincipal CurrentPrincipal(Guid employeeId)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(employeeId);
        principal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        return principal.Object;
    }
}
