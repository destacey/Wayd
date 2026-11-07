using FluentAssertions;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Deliverables.Commands;

public sealed class SetProjectDeliverableTasksCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 5, 1, 0, 0)));
    private readonly SetProjectDeliverableTasksCommandHandler _handler;

    public SetProjectDeliverableTasksCommandHandlerTests()
    {
        _handler = new SetProjectDeliverableTasksCommandHandler(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    /// <summary>A proposed project with a deliverable and two plan tasks, none linked.</summary>
    private (Project Project, ProjectDeliverable Deliverable, ProjectTask Design, ProjectTask Build) ProjectWithPlan()
    {
        var project = new ProjectFaker().AsProposed(_dateTimeProvider);
        var lifecycle = new ProjectLifecycleFaker().AsActiveWithStages(("Plan", "Planning"));
        project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, lifecycle, _dateTimeProvider.Now);
        var stageId = project.Stages.Single().Id;

        var workflow = DeliverableWorkflow.Published(_dateTimeProvider.Now);
        var deliverable = project.AddDeliverable(PpmActor.System, ProjectAncestryRoles.None, "Portal", null, null, null, null,
            workflow.Ref(ProjectDeliverableStatusAlias.NotStarted), _dateTimeProvider.Now).Value;

        ProjectTask Create(int number, string name) => project.CreateTask(number, name, null, ProjectTaskType.Task,
            TaskStatus.NotStarted, TaskPriority.Medium, new Progress(0m), stageId, null, null, null, null).Value;
        var design = Create(1, "Design");
        var build = Create(2, "Build");

        project.ClearDomainEvents();
        _dbContext.AddProject(project);
        return (project, deliverable, design, build);
    }

    [Fact]
    public async Task Handle_LinksTheNamedTasksAndSaves()
    {
        // Arrange
        var (project, deliverable, design, build) = ProjectWithPlan();
        var revision = project.Revision;

        // Act
        var result = await _handler.Handle(new SetProjectDeliverableTasksCommand(project.Id, deliverable.Id, [design.Id, build.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        deliverable.TaskLinks.Select(l => l.ProjectTaskId).Should().BeEquivalentTo([design.Id, build.Id]);
        project.Revision.Should().Be(revision, "linking is plan work and does not advance the revision");
        project.DomainEvents.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithAnEmptyList_UnlinksEveryTask()
    {
        // Arrange
        var (project, deliverable, design, _) = ProjectWithPlan();
        project.SetDeliverableTasks(deliverable.Id, [design.Id]);

        // Act
        var result = await _handler.Handle(new SetProjectDeliverableTasksCommand(project.Id, deliverable.Id, []),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        deliverable.TaskLinks.Should().BeEmpty();
        design.DeliverableLinks.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenATaskIsNotOnTheProject_FailsWithoutSaving()
    {
        // Arrange
        var (project, deliverable, design, _) = ProjectWithPlan();
        var foreignTaskId = Guid.NewGuid();

        // Act
        var result = await _handler.Handle(new SetProjectDeliverableTasksCommand(project.Id, deliverable.Id, [design.Id, foreignTaskId]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(foreignTaskId.ToString());
        deliverable.TaskLinks.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheDeliverableIsNotOnTheProject_ReportsDeliverableNotFound()
    {
        // Arrange
        var (project, _, design, _) = ProjectWithPlan();

        // Act
        var result = await _handler.Handle(new SetProjectDeliverableTasksCommand(project.Id, Guid.NewGuid(), [design.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProjectDeliverableCommandSupport.DeliverableNotFoundError);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheProjectDoesNotExist_ReportsProjectNotFound()
    {
        // Act
        var result = await _handler.Handle(new SetProjectDeliverableTasksCommand(Guid.NewGuid(), Guid.NewGuid(), []),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProjectDeliverableCommandSupport.ProjectNotFoundError);
    }
}
