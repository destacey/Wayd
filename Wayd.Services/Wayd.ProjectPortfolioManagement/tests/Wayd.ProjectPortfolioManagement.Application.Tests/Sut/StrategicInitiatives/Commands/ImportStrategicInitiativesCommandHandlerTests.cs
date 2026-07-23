using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using NodaTime.Extensions;
using Wayd.Common.Domain.Models.KeyPerformanceIndicators;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.StrategicInitiatives.Commands;

public class ImportStrategicInitiativesCommandHandlerTests : IDisposable
{
    private const string PortfolioName = "Growth";

    private static readonly LocalDate _start = new(2024, 7, 1);
    private static readonly LocalDate _end = new(2025, 6, 30);

    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly ImportStrategicInitiativesCommandHandler _handler;
    private readonly Mock<ILogger<ImportStrategicInitiativesCommandHandler>> _mockLogger;
    private readonly TestingDateTimeProvider _dateTimeProvider;

    private readonly ProjectPortfolio _portfolio;

    public ImportStrategicInitiativesCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockLogger = new Mock<ILogger<ImportStrategicInitiativesCommandHandler>>();
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));

        _handler = new ImportStrategicInitiativesCommandHandler(_dbContext, _mockLogger.Object);

        // Initiatives can only be created inside an active portfolio.
        _portfolio = ProjectPortfolio.Create(PortfolioName, "Growth portfolio");
        _portfolio.Activate(_start);
        _dbContext.AddPortfolio(_portfolio);
    }

    [Fact]
    public async Task Handle_ImportsInitiative_IntoThePortfolioNamedOnTheRow()
    {
        // Arrange
        var command = new ImportStrategicInitiativesCommand([Row("Expand to EU", StrategicInitiativeStatus.Proposed)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var initiative = _portfolio.StrategicInitiatives.Single();
        initiative.Name.Should().Be("Expand to EU");
        initiative.Status.Should().Be(StrategicInitiativeStatus.Proposed);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Theory]
    [InlineData(StrategicInitiativeStatus.Approved)]
    [InlineData(StrategicInitiativeStatus.Active)]
    [InlineData(StrategicInitiativeStatus.Completed)]
    [InlineData(StrategicInitiativeStatus.Cancelled)]
    public async Task Handle_DrivesInitiativeToItsTargetStatus(StrategicInitiativeStatus status)
    {
        // Arrange
        // Activation only follows approval, so the whole chain is replayed to reach the later statuses.
        var command = new ImportStrategicInitiativesCommand([Row("Expand to EU", status)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _portfolio.StrategicInitiatives.Single().Status.Should().Be(status);
    }

    [Fact]
    public async Task Handle_Fails_WhenTheStatusIsOnHold()
    {
        // Arrange
        // OnHold is a defined status with no transition that reaches it, so the row is rejected rather
        // than quietly importing a different status.
        var command = new ImportStrategicInitiativesCommand([Row("Expand to EU", StrategicInitiativeStatus.OnHold)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("on hold");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_AttachesProjects_ResolvedByKey()
    {
        // Arrange
        var project = CreateProject("APOLLO");

        var row = Row("Expand to EU", StrategicInitiativeStatus.Active) with { ProjectKeys = ["APOLLO"] };

        // Act
        var result = await _handler.Handle(new ImportStrategicInitiativesCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _portfolio.StrategicInitiatives.Single().StrategicInitiativeProjects
            .Should().ContainSingle(p => p.ProjectId == project.Id);
    }

    [Fact]
    public async Task Handle_AttachesProjects_BeforeClosingTheInitiative()
    {
        // Arrange
        // A closed initiative refuses project changes, so the link has to be made first.
        var project = CreateProject("APOLLO");

        var row = Row("Expand to EU", StrategicInitiativeStatus.Completed) with { ProjectKeys = ["APOLLO"] };

        // Act
        var result = await _handler.Handle(new ImportStrategicInitiativesCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var initiative = _portfolio.StrategicInitiatives.Single();
        initiative.Status.Should().Be(StrategicInitiativeStatus.Completed);
        initiative.StrategicInitiativeProjects.Should().ContainSingle(p => p.ProjectId == project.Id);
    }

    [Fact]
    public async Task Handle_ImportsKpis_AttachedToTheirInitiativeByName()
    {
        // Arrange
        var command = new ImportStrategicInitiativesCommand(
            [Row("Expand to EU", StrategicInitiativeStatus.Active)],
            [Kpi("Expand to EU", "Revenue", 5_000_000)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var kpi = _portfolio.StrategicInitiatives.Single().Kpis.Single();
        kpi.Name.Should().Be("Revenue");
        kpi.TargetValue.Should().Be(5_000_000);
    }

    [Fact]
    public async Task Handle_ImportsKpis_BeforeClosingTheInitiative()
    {
        // Arrange
        var command = new ImportStrategicInitiativesCommand(
            [Row("Expand to EU", StrategicInitiativeStatus.Completed)],
            [Kpi("Expand to EU", "Revenue", 5_000_000)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var initiative = _portfolio.StrategicInitiatives.Single();
        initiative.Status.Should().Be(StrategicInitiativeStatus.Completed);
        initiative.Kpis.Should().ContainSingle(k => k.Name == "Revenue");
    }

    [Fact]
    public async Task Handle_Fails_WhenAKpiNamesAnInitiativeNotInTheImport()
    {
        // Arrange
        var command = new ImportStrategicInitiativesCommand(
            [Row("Expand to EU", StrategicInitiativeStatus.Active)],
            [Kpi("Nonexistent", "Revenue", 5_000_000)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Nonexistent");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_AssignsRoles_ResolvedByEmployeeNumber()
    {
        // Arrange
        var sponsor = new EmployeeFaker().WithEmployeeNumber("E100").Generate();
        _dbContext.AddEmployee(sponsor);

        var row = Row("Expand to EU", StrategicInitiativeStatus.Active) with { SponsorEmployeeNumbers = ["E100"] };

        // Act
        var result = await _handler.Handle(new ImportStrategicInitiativesCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _portfolio.StrategicInitiatives.Single().Roles
            .Should().ContainSingle(r => r.Role == StrategicInitiativeRole.Sponsor && r.EmployeeId == sponsor.Id);
    }

    [Fact]
    public async Task Handle_Fails_WhenAProjectKeyCannotBeResolved()
    {
        // Arrange
        var row = Row("Expand to EU", StrategicInitiativeStatus.Active) with { ProjectKeys = ["GEMINI"] };

        // Act
        var result = await _handler.Handle(new ImportStrategicInitiativesCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("GEMINI");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Fails_WhenANameIsDuplicatedWithinTheBatch()
    {
        // Arrange
        var command = new ImportStrategicInitiativesCommand([
            Row("Expand to EU", StrategicInitiativeStatus.Active),
            Row("expand to eu", StrategicInitiativeStatus.Active),
        ]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("more than once");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    private Project CreateProject(string key)
    {
        var project = _portfolio.CreateProject(
            $"Project {key}",
            $"{key} description",
            new ProjectKey(key),
            1,
            new LocalDateRange(_start, _end),
            null,
            null,
            null,
            null,
            null,
            _dateTimeProvider.Now).Value;

        _dbContext.AddProject(project);

        return project;
    }

    private static ImportStrategicInitiativeDto Row(string name, StrategicInitiativeStatus status) =>
        new(name, $"{name} initiative", status, PortfolioName, _start, _end, [], [], []);

    private static ImportStrategicInitiativeKpiDto Kpi(string initiativeName, string name, double targetValue) =>
        new(initiativeName, name, null, targetValue, 0, null, null, KpiTargetDirection.Increase);

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
