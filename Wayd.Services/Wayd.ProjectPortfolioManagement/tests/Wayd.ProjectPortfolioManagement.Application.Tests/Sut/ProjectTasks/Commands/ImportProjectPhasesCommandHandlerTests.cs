using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using NodaTime.Extensions;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.ProjectTasks.Commands;

public class ImportProjectPhasesCommandHandlerTests : IDisposable
{
    private const string ProjectKeyValue = "APOLLO";
    private const string PhaseName = "Build";

    private static readonly LocalDate _start = new(2024, 7, 1);
    private static readonly LocalDate _end = new(2025, 6, 30);

    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly ImportProjectPhasesCommandHandler _handler;
    private readonly Mock<ILogger<ImportProjectPhasesCommandHandler>> _mockLogger;
    private readonly TestingDateTimeProvider _dateTimeProvider;

    private readonly Project _project;

    public ImportProjectPhasesCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockLogger = new Mock<ILogger<ImportProjectPhasesCommandHandler>>();
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));

        _handler = new ImportProjectPhasesCommandHandler(_dbContext, _mockLogger.Object);

        // A project with an assigned lifecycle, which is where its phases come from.
        var portfolio = ProjectPortfolio.Create("Growth", "Growth portfolio");
        portfolio.Activate(_start);

        _project = portfolio.CreateProject(
            "Project Apollo",
            "Apollo description",
            new ProjectKey(ProjectKeyValue),
            1,
            new LocalDateRange(_start, _end),
            null,
            null,
            null,
            null,
            null,
            _dateTimeProvider.Now).Value;

        var lifecycle = new ProjectLifecycleFaker().WithName("Standard").AsActiveWithPhases((PhaseName, "Delivery"), ("Close", "Closure"));
        _project.AssignLifecycle(lifecycle);

        _dbContext.AddProject(_project);
    }

    [Fact]
    public async Task Handle_SetsThePhaseStatusExactlyAsGiven()
    {
        // Arrange — the status is applied verbatim, not derived from any tasks.
        var command = new ImportProjectPhasesCommand([Row(PhaseName, TaskStatus.Completed)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _project.Phases.Single(p => p.Name == PhaseName).Status.Should().Be(TaskStatus.Completed);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SetsEachPhaseIndependently()
    {
        // Arrange
        var command = new ImportProjectPhasesCommand([
            Row("Build", TaskStatus.Completed),
            Row("Close", TaskStatus.InProgress),
        ]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _project.Phases.Single(p => p.Name == "Build").Status.Should().Be(TaskStatus.Completed);
        _project.Phases.Single(p => p.Name == "Close").Status.Should().Be(TaskStatus.InProgress);
    }

    [Fact]
    public async Task Handle_Fails_WhenTheProjectCannotBeResolved()
    {
        // Arrange
        var row = Row(PhaseName, TaskStatus.Completed) with { ProjectKey = new ProjectKey("GEMINI") };

        // Act
        var result = await _handler.Handle(new ImportProjectPhasesCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("GEMINI");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Fails_WhenThePhaseIsNotOnTheProjectsLifecycle()
    {
        // Arrange
        var row = Row("Nonexistent", TaskStatus.Completed);

        // Act
        var result = await _handler.Handle(new ImportProjectPhasesCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Nonexistent");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    private static ImportProjectPhaseDto Row(string phaseName, TaskStatus status) =>
        new(new ProjectKey(ProjectKeyValue), phaseName, status);

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
