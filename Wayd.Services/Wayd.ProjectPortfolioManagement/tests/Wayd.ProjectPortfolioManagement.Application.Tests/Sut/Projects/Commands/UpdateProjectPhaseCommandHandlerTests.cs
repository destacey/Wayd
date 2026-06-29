using FluentAssertions;
using Microsoft.Extensions.Logging;
using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using Moq;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.Common.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Commands;

public class UpdateProjectPhaseCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly UpdateProjectPhaseCommandHandler _handler;
    private readonly Mock<ILogger<UpdateProjectPhaseCommandHandler>> _mockLogger;
    private readonly TestingDateTimeProvider _dateTimeProvider;

    private readonly ProjectFaker _projectFaker;
    private readonly ProjectLifecycleFaker _lifecycleFaker;

    public UpdateProjectPhaseCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockLogger = new Mock<ILogger<UpdateProjectPhaseCommandHandler>>();
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));

        _handler = new UpdateProjectPhaseCommandHandler(_dbContext, _mockLogger.Object);

        _projectFaker = new ProjectFaker();
        _lifecycleFaker = new ProjectLifecycleFaker();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenPhaseDoesNotExist()
    {
        // Arrange
        var command = new UpdateProjectPhaseCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Updated description",
            (int)TaskStatus.InProgress,
            null,
            null,
            50m,
            null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldUpdatePhase_WhenPhaseExists()
    {
        // Arrange
        var project = _projectFaker.AsProposed(_dateTimeProvider);
        var lifecycle = _lifecycleFaker.AsActiveWithPhases(("Plan", "Planning"), ("Execute", "Execution"), ("Deliver", "Delivery"));
        project.AssignLifecycle(lifecycle);
        _dbContext.AddProject(project);
        _dbContext.AddProjectPhases(project.Phases);

        var phase = project.Phases.First();
        var plannedStart = new LocalDate(2026, 4, 1);
        var plannedEnd = new LocalDate(2026, 6, 30);

        var command = new UpdateProjectPhaseCommand(
            project.Id,
            phase.Id,
            "Updated phase description",
            (int)TaskStatus.InProgress,
            plannedStart,
            plannedEnd,
            45m,
            null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        phase.Description.Should().Be("Updated phase description");
        phase.Status.Should().Be(TaskStatus.InProgress);
        phase.DateRange.Should().NotBeNull();
        phase.DateRange!.Start.Should().Be(plannedStart);
        phase.DateRange!.End.Should().Be(plannedEnd);
        phase.Progress.Value.Should().Be(45m);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldUpdatePhase_WithNullDates()
    {
        // Arrange
        var project = _projectFaker.AsProposed(_dateTimeProvider);
        var lifecycle = _lifecycleFaker.AsActiveWithPhases(("Plan", "Planning"), ("Execute", "Execution"), ("Deliver", "Delivery"));
        project.AssignLifecycle(lifecycle);
        _dbContext.AddProject(project);
        _dbContext.AddProjectPhases(project.Phases);

        var phase = project.Phases.First();

        var command = new UpdateProjectPhaseCommand(
            project.Id,
            phase.Id,
            "Updated description with no dates",
            (int)TaskStatus.NotStarted,
            null,
            null,
            0m,
            null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        phase.Description.Should().Be("Updated description with no dates");
        phase.Status.Should().Be(TaskStatus.NotStarted);
        phase.DateRange.Should().BeNull();
        phase.Progress.Value.Should().Be(0m);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenPhaseDateClearedButDatedRootTasksExist()
    {
        // Arrange
        var project = _projectFaker.AsProposed(_dateTimeProvider);
        var lifecycle = _lifecycleFaker.AsActiveWithPhases(("Plan", "Planning"));
        project.AssignLifecycle(lifecycle);
        var phase = project.Phases.First();
        
        var rootTaskRange = new FlexibleDateRange(new LocalDate(2026, 6, 8), new LocalDate(2026, 6, 12));
        project.CreateTask(1, "Root Task", null, ProjectTaskType.Task, TaskStatus.NotStarted, TaskPriority.Medium, new Progress(0m), phase.Id, rootTaskRange, null, null, null);

        _dbContext.AddProject(project);
        _dbContext.AddProjectPhases(project.Phases);
        _dbContext.AddProjectTasks(project.Tasks);

        var command = new UpdateProjectPhaseCommand(
            project.Id,
            phase.Id,
            "Clear Dates Phase",
            (int)TaskStatus.NotStarted,
            null, // Clear PlannedStart
            null, // Clear PlannedEnd
            0m,
            null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be updated to null");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenPhaseRangeShrunkAndExcludesDatedRootTasks()
    {
        // Arrange
        var project = _projectFaker.AsProposed(_dateTimeProvider);
        var lifecycle = _lifecycleFaker.AsActiveWithPhases(("Plan", "Planning"));
        project.AssignLifecycle(lifecycle);
        var phase = project.Phases.First();
        
        var rootTaskRange = new FlexibleDateRange(new LocalDate(2026, 6, 8), new LocalDate(2026, 6, 12));
        project.CreateTask(1, "Root Task", null, ProjectTaskType.Task, TaskStatus.NotStarted, TaskPriority.Medium, new Progress(0m), phase.Id, rootTaskRange, null, null, null);

        _dbContext.AddProject(project);
        _dbContext.AddProjectPhases(project.Phases);
        _dbContext.AddProjectTasks(project.Tasks);

        var shrunkStart = new LocalDate(2026, 6, 9); // Excludes task start on 8
        var command = new UpdateProjectPhaseCommand(
            project.Id,
            phase.Id,
            "Shrunk Dates Phase",
            (int)TaskStatus.NotStarted,
            shrunkStart,
            new LocalDate(2026, 6, 12),
            0m,
            null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("falls outside the selected range");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
