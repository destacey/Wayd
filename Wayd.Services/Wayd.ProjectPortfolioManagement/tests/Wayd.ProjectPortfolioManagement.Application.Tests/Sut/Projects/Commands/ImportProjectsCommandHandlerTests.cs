using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using NodaTime.Extensions;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.Common.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Commands;

public class ImportProjectsCommandHandlerTests : IDisposable
{
    private const string PortfolioName = "Growth";
    private const string CategoryName = "Capex";

    private static readonly LocalDate _start = new(2024, 7, 1);
    private static readonly LocalDate _end = new(2025, 6, 30);

    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly ImportProjectsCommandHandler _handler;
    private readonly Mock<ILogger<ImportProjectsCommandHandler>> _mockLogger;
    private readonly TestingDateTimeProvider _dateTimeProvider;

    private readonly ProjectPortfolio _portfolio;

    public ImportProjectsCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockLogger = new Mock<ILogger<ImportProjectsCommandHandler>>();
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));

        _handler = new ImportProjectsCommandHandler(_dbContext, _dateTimeProvider, _mockLogger.Object);

        // A project can only be created inside an active portfolio, so every case starts from one.
        _portfolio = ProjectPortfolio.Create(PortfolioName, "Growth portfolio");
        _portfolio.Activate(_start);
        _dbContext.AddPortfolio(_portfolio);

        _dbContext.AddExpenditureCategory(new ExpenditureCategoryFaker().WithName(CategoryName).Generate());
    }

    [Fact]
    public async Task Handle_ImportsProject_ResolvingPortfolioAndCategoryByName()
    {
        // Arrange
        var command = new ImportProjectsCommand([Row("APOLLO", ProjectStatus.Proposed, start: null)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var project = _portfolio.Projects.Single();
        project.Key.Value.Should().Be("APOLLO");
        project.Status.Should().Be(ProjectStatus.Proposed);
        project.PortfolioId.Should().Be(_portfolio.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Theory]
    [InlineData(ProjectStatus.Active)]
    [InlineData(ProjectStatus.Completed)]
    [InlineData(ProjectStatus.Cancelled)]
    public async Task Handle_DrivesProjectToItsTargetStatus(ProjectStatus status)
    {
        // Arrange
        // Unlike programs and portfolios, a project has nothing beneath it to close first, so it reaches
        // its true final status during the import.
        var command = new ImportProjectsCommand([Row("APOLLO", status, _start, _end)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _portfolio.Projects.Single().Status.Should().Be(status);
    }

    [Fact]
    public async Task Handle_AssignsLifecycleAndCopiesItsPhases()
    {
        // Arrange
        // The copied phases are what project tasks are later imported into.
        var lifecycle = new ProjectLifecycleFaker().WithName("Standard").AsActiveWithPhases(("Plan", "Planning"), ("Build", "Delivery"));
        _dbContext.AddProjectLifecycle(lifecycle);

        var row = Row("APOLLO", ProjectStatus.Proposed, start: null) with { ProjectLifecycleName = "Standard" };

        // Act
        var result = await _handler.Handle(new ImportProjectsCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var project = _portfolio.Projects.Single();
        project.ProjectLifecycleId.Should().Be(lifecycle.Id);
        project.Phases.Select(p => p.Name).Should().Equal("Plan", "Build");
    }

    [Fact]
    public async Task Handle_ApprovesProject_WhenALifecycleIsAssigned()
    {
        // Arrange
        // Approval is refused without a lifecycle, so the two have to be applied in that order.
        var lifecycle = new ProjectLifecycleFaker().WithName("Standard").AsActiveWithPhases(("Plan", "Planning"));
        _dbContext.AddProjectLifecycle(lifecycle);

        var row = Row("APOLLO", ProjectStatus.Approved, start: null) with { ProjectLifecycleName = "Standard" };

        // Act
        var result = await _handler.Handle(new ImportProjectsCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _portfolio.Projects.Single().Status.Should().Be(ProjectStatus.Approved);
    }

    [Fact]
    public async Task Handle_AttachesProjectToProgram_ResolvedWithinItsPortfolio()
    {
        // Arrange
        var program = _portfolio.CreateProgram("Platform", "Platform program", new LocalDateRange(_start, _end), null, null, _dateTimeProvider.Now).Value;
        program.Activate();

        var row = Row("APOLLO", ProjectStatus.Proposed, start: null) with { ProgramName = "Platform" };

        // Act
        var result = await _handler.Handle(new ImportProjectsCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _portfolio.Projects.Single().ProgramId.Should().Be(program.Id);
    }

    [Fact]
    public async Task Handle_RanksProjectsSequentially_WithinTheirPortfolio()
    {
        // Arrange
        // Each project is ranked at the bottom, so the running max has to advance across the batch —
        // otherwise every imported project would share a rank.
        var command = new ImportProjectsCommand([
            Row("APOLLO", ProjectStatus.Proposed, start: null),
            Row("GEMINI", ProjectStatus.Proposed, start: null),
            Row("MERCURY", ProjectStatus.Proposed, start: null),
        ]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var ranks = _portfolio.Projects.Select(p => p.Rank).ToList();
        ranks.Should().OnlyHaveUniqueItems();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_AssignsRoles_ResolvedByEmployeeNumber()
    {
        // Arrange
        var manager = new EmployeeFaker().WithEmployeeNumber("E100").Generate();
        var member = new EmployeeFaker().WithEmployeeNumber("E200").Generate();
        _dbContext.AddEmployees([manager, member]);

        var row = Row("APOLLO", ProjectStatus.Proposed, start: null) with
        {
            ManagerEmployeeNumbers = ["E100"],
            MemberEmployeeNumbers = ["E200"],
        };

        // Act
        var result = await _handler.Handle(new ImportProjectsCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var roles = _portfolio.Projects.Single().Roles;
        roles.Should().ContainSingle(r => r.Role == ProjectRole.Manager && r.EmployeeId == manager.Id);
        roles.Should().ContainSingle(r => r.Role == ProjectRole.Member && r.EmployeeId == member.Id);
    }

    [Fact]
    public async Task Handle_Fails_WhenAKeyIsDuplicatedWithinTheBatch()
    {
        // Arrange
        var command = new ImportProjectsCommand([
            Row("APOLLO", ProjectStatus.Proposed, start: null),
            Row("APOLLO", ProjectStatus.Proposed, start: null),
        ]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("more than once");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Fails_WhenTheProjectKeyAlreadyExists()
    {
        // Arrange
        // Project keys are unique in the database, so the clash is caught before the batch is applied.
        _dbContext.AddProject(new ProjectFaker().WithKey(new ProjectKey("APOLLO")).Generate());

        var command = new ImportProjectsCommand([Row("APOLLO", ProjectStatus.Proposed, start: null)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exist");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Fails_WhenThePortfolioCannotBeResolved()
    {
        // Arrange
        var row = Row("APOLLO", ProjectStatus.Proposed, start: null) with { PortfolioName = "Nonexistent" };

        // Act
        var result = await _handler.Handle(new ImportProjectsCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Nonexistent");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Fails_WhenTheProgramIsNotInTheProjectsPortfolio()
    {
        // Arrange
        // Program names are only unique within a portfolio, so resolution is scoped to it.
        var row = Row("APOLLO", ProjectStatus.Proposed, start: null) with { ProgramName = "Elsewhere" };

        // Act
        var result = await _handler.Handle(new ImportProjectsCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Elsewhere");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Fails_WhenTheExpenditureCategoryCannotBeResolved()
    {
        // Arrange
        var row = Row("APOLLO", ProjectStatus.Proposed, start: null) with { ExpenditureCategoryName = "Nonexistent" };

        // Act
        var result = await _handler.Handle(new ImportProjectsCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Nonexistent");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    private static ImportProjectDto Row(string key, ProjectStatus status, LocalDate? start, LocalDate? end = null) =>
        new(
            $"Project {key}",
            $"{key} description",
            new ProjectKey(key),
            status,
            PortfolioName,
            null,
            CategoryName,
            null,
            null,
            null,
            start,
            end,
            [],
            [],
            [],
            [],
            []);

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
