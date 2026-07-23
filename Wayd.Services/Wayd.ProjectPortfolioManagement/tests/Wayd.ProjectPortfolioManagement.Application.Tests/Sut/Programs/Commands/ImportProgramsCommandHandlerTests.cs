using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using NodaTime.Extensions;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Application.Programs.Commands;
using Wayd.ProjectPortfolioManagement.Application.Programs.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Programs.Commands;

public class ImportProgramsCommandHandlerTests : IDisposable
{
    private const string PortfolioName = "Growth";

    private static readonly LocalDate _start = new(2024, 7, 1);
    private static readonly LocalDate _end = new(2025, 6, 30);

    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly ImportProgramsCommandHandler _handler;
    private readonly Mock<ILogger<ImportProgramsCommandHandler>> _mockLogger;
    private readonly TestingDateTimeProvider _dateTimeProvider;

    private readonly ProjectPortfolio _portfolio;

    public ImportProgramsCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockLogger = new Mock<ILogger<ImportProgramsCommandHandler>>();
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));

        _handler = new ImportProgramsCommandHandler(_dbContext, _dateTimeProvider, _mockLogger.Object);

        // Programs can only be created inside an active portfolio.
        _portfolio = ProjectPortfolio.Create(PortfolioName, "Growth portfolio");
        _portfolio.Activate(_start);
        _dbContext.AddPortfolio(_portfolio);
    }

    [Fact]
    public async Task Handle_ImportsProgram_IntoThePortfolioNamedOnTheRow()
    {
        // Arrange
        var command = new ImportProgramsCommand([Row("Platform", ProgramStatus.Active)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var program = _portfolio.Programs.Single();
        program.Name.Should().Be("Platform");
        program.Status.Should().Be(ProgramStatus.Active);
        program.PortfolioId.Should().Be(_portfolio.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_LeavesProgramProposed_WhenStatusIsProposed()
    {
        // Arrange
        var command = new ImportProgramsCommand([Row("Platform", ProgramStatus.Proposed)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _portfolio.Programs.Single().Status.Should().Be(ProgramStatus.Proposed);
    }

    [Theory]
    [InlineData(ProgramStatus.Completed)]
    [InlineData(ProgramStatus.Cancelled)]
    public async Task Handle_LeavesProgramActive_WhenStatusIsTerminal(ProgramStatus status)
    {
        // Arrange
        // A program only accepts projects while active and can only close once they are all closed, so the
        // finalize import finishes it after the projects land.
        var command = new ImportProgramsCommand([Row("Platform", status)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _portfolio.Programs.Single().Status.Should().Be(ProgramStatus.Active);
    }

    [Fact]
    public async Task Handle_AttachesStrategicThemes_ResolvedByName()
    {
        // Arrange
        var theme = new StrategicThemeFaker().WithName("Reliability").Generate();
        _dbContext.AddPpmStrategicTheme(theme);

        var row = Row("Platform", ProgramStatus.Active) with { StrategicThemeNames = ["Reliability"] };

        // Act
        var result = await _handler.Handle(new ImportProgramsCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _portfolio.Programs.Single().StrategicThemeTags.Should().ContainSingle(t => t.StrategicThemeId == theme.Id);
    }

    [Fact]
    public async Task Handle_Fails_WhenAStrategicThemeIsNotActive()
    {
        // Arrange
        // Only active themes can be attached, so an archived one is reported rather than dropped.
        var theme = new StrategicThemeFaker().WithName("Retired").WithState(StrategicThemeState.Archived).Generate();
        _dbContext.AddPpmStrategicTheme(theme);

        var row = Row("Platform", ProgramStatus.Active) with { StrategicThemeNames = ["Retired"] };

        // Act
        var result = await _handler.Handle(new ImportProgramsCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not active");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_AssignsRoles_ResolvedByEmployeeNumber()
    {
        // Arrange
        var manager = new EmployeeFaker().WithEmployeeNumber("E100").Generate();
        _dbContext.AddEmployee(manager);

        var row = Row("Platform", ProgramStatus.Active) with { ManagerEmployeeNumbers = ["E100"] };

        // Act
        var result = await _handler.Handle(new ImportProgramsCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _portfolio.Programs.Single().Roles
            .Should().ContainSingle(r => r.Role == ProgramRole.Manager && r.EmployeeId == manager.Id);
    }

    [Fact]
    public async Task Handle_Fails_WhenThePortfolioCannotBeResolved()
    {
        // Arrange
        var row = Row("Platform", ProgramStatus.Active) with { PortfolioName = "Nonexistent" };

        // Act
        var result = await _handler.Handle(new ImportProgramsCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Nonexistent");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Fails_WhenANameIsDuplicatedWithinTheBatch()
    {
        // Arrange
        var command = new ImportProgramsCommand([
            Row("Platform", ProgramStatus.Active),
            Row("platform", ProgramStatus.Active),
        ]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("more than once");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    private static ImportProgramDto Row(string name, ProgramStatus status) =>
        new(name, $"{name} program", status, PortfolioName, _start, _end, [], [], [], []);

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
