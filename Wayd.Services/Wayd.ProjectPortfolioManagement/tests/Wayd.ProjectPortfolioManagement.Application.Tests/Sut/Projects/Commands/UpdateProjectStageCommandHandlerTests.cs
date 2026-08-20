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

using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Commands;

public class UpdateProjectStageCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly UpdateProjectStageCommandHandler _handler;
    private readonly Mock<ILogger<UpdateProjectStageCommandHandler>> _mockLogger;
    private readonly TestingDateTimeProvider _dateTimeProvider;

    private readonly ProjectFaker _projectFaker;
    private readonly ProjectLifecycleFaker _lifecycleFaker;

    public UpdateProjectStageCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockLogger = new Mock<ILogger<UpdateProjectStageCommandHandler>>();
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));

        _handler = new UpdateProjectStageCommandHandler(_dbContext, _mockLogger.Object);

        _projectFaker = new ProjectFaker();
        _lifecycleFaker = new ProjectLifecycleFaker();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenStageDoesNotExist()
    {
        // Arrange
        var command = new UpdateProjectStageCommand(
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
    public async Task Handle_ShouldUpdateStage_WhenStageExists()
    {
        // Arrange
        var project = _projectFaker.AsProposed(_dateTimeProvider);
        var lifecycle = _lifecycleFaker.AsActiveWithStages(("Plan", "Planning"), ("Execute", "Execution"), ("Deliver", "Delivery"));
        project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, lifecycle);
        _dbContext.AddProject(project);
        _dbContext.AddProjectStages(project.Stages);

        var stage = project.Stages.First();
        var plannedStart = new LocalDate(2026, 4, 1);
        var plannedEnd = new LocalDate(2026, 6, 30);

        var command = new UpdateProjectStageCommand(
            project.Id,
            stage.Id,
            "Updated stage description",
            (int)TaskStatus.InProgress,
            plannedStart,
            plannedEnd,
            45m,
            null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        stage.Description.Should().Be("Updated stage description");
        stage.Status.Should().Be(TaskStatus.InProgress);
        stage.DateRange.Should().NotBeNull();
        stage.DateRange!.Start.Should().Be(plannedStart);
        stage.DateRange!.End.Should().Be(plannedEnd);
        stage.Progress.Value.Should().Be(45m);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldUpdateStage_WithNullDates()
    {
        // Arrange
        var project = _projectFaker.AsProposed(_dateTimeProvider);
        var lifecycle = _lifecycleFaker.AsActiveWithStages(("Plan", "Planning"), ("Execute", "Execution"), ("Deliver", "Delivery"));
        project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, lifecycle);
        _dbContext.AddProject(project);
        _dbContext.AddProjectStages(project.Stages);

        var stage = project.Stages.First();

        var command = new UpdateProjectStageCommand(
            project.Id,
            stage.Id,
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
        stage.Description.Should().Be("Updated description with no dates");
        stage.Status.Should().Be(TaskStatus.NotStarted);
        stage.DateRange.Should().BeNull();
        stage.Progress.Value.Should().Be(0m);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenStageDateClearedButDatedRootTasksExist()
    {
        // Arrange
        var project = _projectFaker.AsProposed(_dateTimeProvider);
        var lifecycle = _lifecycleFaker.AsActiveWithStages(("Plan", "Planning"));
        project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, lifecycle);
        var stage = project.Stages.First();
        
        var rootTaskRange = new FlexibleDateRange(new LocalDate(2026, 6, 8), new LocalDate(2026, 6, 12));
        project.CreateTask(1, "Root Task", null, ProjectTaskType.Task, TaskStatus.NotStarted, TaskPriority.Medium, new Progress(0m), stage.Id, rootTaskRange, null, null, null);

        _dbContext.AddProject(project);
        _dbContext.AddProjectStages(project.Stages);
        _dbContext.AddProjectTasks(project.Tasks);

        var command = new UpdateProjectStageCommand(
            project.Id,
            stage.Id,
            "Clear Dates Stage",
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
    public async Task Handle_ShouldFail_WhenStageRangeShrunkAndExcludesDatedRootTasks()
    {
        // Arrange
        var project = _projectFaker.AsProposed(_dateTimeProvider);
        var lifecycle = _lifecycleFaker.AsActiveWithStages(("Plan", "Planning"));
        project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, lifecycle);
        var stage = project.Stages.First();
        
        var rootTaskRange = new FlexibleDateRange(new LocalDate(2026, 6, 8), new LocalDate(2026, 6, 12));
        project.CreateTask(1, "Root Task", null, ProjectTaskType.Task, TaskStatus.NotStarted, TaskPriority.Medium, new Progress(0m), stage.Id, rootTaskRange, null, null, null);

        _dbContext.AddProject(project);
        _dbContext.AddProjectStages(project.Stages);
        _dbContext.AddProjectTasks(project.Tasks);

        var shrunkStart = new LocalDate(2026, 6, 9); // Excludes task start on 8
        var command = new UpdateProjectStageCommand(
            project.Id,
            stage.Id,
            "Shrunk Dates Stage",
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
