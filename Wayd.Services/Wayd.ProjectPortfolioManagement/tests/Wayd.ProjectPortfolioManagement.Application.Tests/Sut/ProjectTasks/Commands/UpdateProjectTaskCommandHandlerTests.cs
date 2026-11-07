using FluentAssertions;
using Microsoft.Extensions.Logging;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using Moq;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;

using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.ProjectTasks.Commands;

public class UpdateProjectTaskCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly UpdateProjectTaskCommandHandler _handler;
    private readonly Mock<ILogger<UpdateProjectTaskCommandHandler>> _mockLogger;
    private readonly TestingDateTimeProvider _dateTimeProvider;

    private readonly ProjectFaker _projectFaker;
    private readonly ProjectLifecycleFaker _lifecycleFaker;

    public UpdateProjectTaskCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockLogger = new Mock<ILogger<UpdateProjectTaskCommandHandler>>();
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));

        _handler = new UpdateProjectTaskCommandHandler(_dbContext, _mockLogger.Object, _dateTimeProvider);

        _projectFaker = new ProjectFaker();
        _lifecycleFaker = new ProjectLifecycleFaker();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTaskDoesNotExist()
    {
        // Arrange
        var command = new UpdateProjectTaskCommand(
            Guid.NewGuid(),
            "Task Name",
            null,
            TaskStatus.NotStarted,
            TaskPriority.Medium,
            null,
            Guid.NewGuid(), // ParentId
            null, // PlannedDateRange
            null, // PlannedDate
            null, // EstimatedEffortHours
            null); // AssigneeIds

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTaskUpdatedToNullButDatedChildrenExist()
    {
        // Arrange
        var project = _projectFaker.AsProposed(_dateTimeProvider);
        var lifecycle = _lifecycleFaker.AsActiveWithStages(("Plan", "Planning"));
        project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, lifecycle, _dateTimeProvider.Now);
        var stage = project.Stages.First();
        
        var parentRange = new FlexibleDateRange(new LocalDate(2026, 6, 5), new LocalDate(2026, 6, 15));
        var parentTask = project.CreateTask(1, "Parent Task", null, ProjectTaskType.Task, TaskStatus.NotStarted, TaskPriority.Medium, new Progress(0m), stage.Id, parentRange, null, null, null).Value;

        var childRange = new FlexibleDateRange(new LocalDate(2026, 6, 8), new LocalDate(2026, 6, 12));
        project.CreateTask(2, "Child Task", null, ProjectTaskType.Task, TaskStatus.NotStarted, TaskPriority.Medium, new Progress(0m), parentTask.Id, childRange, null, null, null);

        project.LinkTaskParents();
        _dbContext.AddProject(project);
        _dbContext.AddProjectStages(project.Stages);
        _dbContext.AddProjectTasks(project.Tasks);

        var command = new UpdateProjectTaskCommand(
            parentTask.Id,
            "Parent Task",
            null,
            TaskStatus.NotStarted,
            TaskPriority.Medium,
            new Progress(0m), // Progress
            stage.Id, // ParentId
            null, // Clear dates
            null,
            null,
            null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be updated to null");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTaskRangeResizedAndExcludesDatedChildren()
    {
        // Arrange
        var project = _projectFaker.AsProposed(_dateTimeProvider);
        var lifecycle = _lifecycleFaker.AsActiveWithStages(("Plan", "Planning"));
        project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, lifecycle, _dateTimeProvider.Now);
        var stage = project.Stages.First();
        
        var parentRange = new FlexibleDateRange(new LocalDate(2026, 6, 5), new LocalDate(2026, 6, 15));
        var parentTask = project.CreateTask(1, "Parent Task", null, ProjectTaskType.Task, TaskStatus.NotStarted, TaskPriority.Medium, new Progress(0m), stage.Id, parentRange, null, null, null).Value;

        var childRange = new FlexibleDateRange(new LocalDate(2026, 6, 8), new LocalDate(2026, 6, 12));
        project.CreateTask(2, "Child Task", null, ProjectTaskType.Task, TaskStatus.NotStarted, TaskPriority.Medium, new Progress(0m), parentTask.Id, childRange, null, null, null);

        project.LinkTaskParents();
        _dbContext.AddProject(project);
        _dbContext.AddProjectStages(project.Stages);
        _dbContext.AddProjectTasks(project.Tasks);

        var shrunkRange = new FlexibleDateRange(new LocalDate(2026, 6, 9), new LocalDate(2026, 6, 15)); // Excludes child start on 8

        var command = new UpdateProjectTaskCommand(
            parentTask.Id,
            "Parent Task",
            null,
            TaskStatus.NotStarted,
            TaskPriority.Medium,
            new Progress(0m), // Progress
            stage.Id, // ParentId
            shrunkRange,
            null,
            null,
            null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("falls outside the selected range");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldSucceedAndShiftChildren_WhenTaskRangeShifted()
    {
        // Arrange
        var project = _projectFaker.AsProposed(_dateTimeProvider);
        var lifecycle = _lifecycleFaker.AsActiveWithStages(("Plan", "Planning"));
        project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, lifecycle, _dateTimeProvider.Now);
        var stage = project.Stages.First();
        
        var parentRange = new FlexibleDateRange(new LocalDate(2026, 6, 5), new LocalDate(2026, 6, 15));
        var parentTask = project.CreateTask(1, "Parent Task", null, ProjectTaskType.Task, TaskStatus.NotStarted, TaskPriority.Medium, new Progress(0m), stage.Id, parentRange, null, null, null).Value;

        var childRange = new FlexibleDateRange(new LocalDate(2026, 6, 8), new LocalDate(2026, 6, 12));
        var childTask = project.CreateTask(2, "Child Task", null, ProjectTaskType.Task, TaskStatus.NotStarted, TaskPriority.Medium, new Progress(0m), parentTask.Id, childRange, null, null, null).Value;

        project.LinkTaskParents();
        _dbContext.AddProject(project);
        _dbContext.AddProjectStages(project.Stages);
        _dbContext.AddProjectTasks(project.Tasks);

        var shiftedRange = new FlexibleDateRange(new LocalDate(2026, 6, 10), new LocalDate(2026, 6, 20)); // Shift +5 days

        var command = new UpdateProjectTaskCommand(
            parentTask.Id,
            "Parent Task",
            null,
            TaskStatus.NotStarted,
            TaskPriority.Medium,
            new Progress(0m), // Progress
            stage.Id, // ParentId
            shiftedRange,
            null,
            null,
            null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        parentTask.PlannedDateRange!.Start.Should().Be(new LocalDate(2026, 6, 10));
        parentTask.PlannedDateRange.End.Should().Be(new LocalDate(2026, 6, 20));

        childTask.PlannedDateRange!.Start.Should().Be(new LocalDate(2026, 6, 13)); // 8 + 5
        childTask.PlannedDateRange.End.Should().Be(new LocalDate(2026, 6, 17)); // 12 + 5
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    /// <summary>A proposed project with one task, linked to the first of two deliverables.</summary>
    private (ProjectTask Task, ProjectDeliverable Portal, ProjectDeliverable Reports, Guid StageId) TaskServingThePortal()
    {
        var project = _projectFaker.AsProposed(_dateTimeProvider);
        var lifecycle = _lifecycleFaker.AsActiveWithStages(("Plan", "Planning"));
        project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, lifecycle, _dateTimeProvider.Now);
        var stage = project.Stages.First();

        var workflow = DeliverableWorkflow.Published(_dateTimeProvider.Now);
        ProjectDeliverable Add(string name) => project.AddDeliverable(PpmActor.System, ProjectAncestryRoles.None, name, null, null, null, null,
            workflow.Ref(ProjectDeliverableStatusAlias.NotStarted), _dateTimeProvider.Now).Value;
        var portal = Add("Portal");
        var reports = Add("Reports");

        var task = project.CreateTask(1, "Build", null, ProjectTaskType.Task, TaskStatus.NotStarted, TaskPriority.Medium, new Progress(0m), stage.Id, null, null, null, null).Value;
        project.SetTaskDeliverables(task.Id, [portal.Id]);

        _dbContext.AddProject(project);
        _dbContext.AddProjectTasks(project.Tasks);
        return (task, portal, reports, stage.Id);
    }

    private static UpdateProjectTaskCommand Unchanged(ProjectTask task, Guid stageId, List<Guid>? deliverableIds) =>
        new(task.Id, task.Name, null, TaskStatus.NotStarted, TaskPriority.Medium, new Progress(0m), stageId, null, null, null, null, deliverableIds);

    [Fact]
    public async Task Handle_WithDeliverableIds_ReplacesTheTasksLinks()
    {
        // Arrange
        var (task, portal, reports, stageId) = TaskServingThePortal();

        // Act
        var result = await _handler.Handle(Unchanged(task, stageId, [reports.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        task.DeliverableLinks.Select(l => l.ProjectDeliverableId).Should().Equal(reports.Id);
        portal.TaskLinks.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithoutDeliverableIds_LeavesTheTasksLinks()
    {
        // Arrange
        var (task, portal, _, stageId) = TaskServingThePortal();

        // Act
        var result = await _handler.Handle(Unchanged(task, stageId, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        task.DeliverableLinks.Select(l => l.ProjectDeliverableId).Should().Equal(portal.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenADeliverableIsNotOnTheProject_FailsWithoutSaving()
    {
        // Arrange
        var (task, portal, _, stageId) = TaskServingThePortal();
        var foreignDeliverableId = Guid.NewGuid();

        // Act
        var result = await _handler.Handle(Unchanged(task, stageId, [foreignDeliverableId]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(foreignDeliverableId.ToString());
        task.DeliverableLinks.Select(l => l.ProjectDeliverableId).Should().Equal(portal.Id);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
