using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Queries;

public sealed class GetMyProjectTasksQueryHandlerTests : IDisposable
{
    private static readonly LocalDate Today = new(2026, 3, 18);

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _employeeId = Guid.NewGuid();
    private readonly Guid _otherEmployeeId = Guid.NewGuid();
    private readonly GetMyProjectTasksQueryHandler _handler;

    public GetMyProjectTasksQueryHandlerTests()
    {
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
        _handler = new GetMyProjectTasksQueryHandler(_dbContext, _currentPrincipal.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    private Project AddProject(ProjectStatus status = ProjectStatus.Active, string? name = null, Dictionary<ProjectRole, HashSet<Guid>>? roles = null)
    {
        var faker = new ProjectFaker().WithStatus(status).WithRoles(roles);
        if (name is not null)
            faker = faker.WithName(name);
        var project = faker.Generate();
        _dbContext.AddProject(project);
        return project;
    }

    private ProjectTask AddTask(Project project, LocalDate? end, Guid assignee, TaskStatus status = TaskStatus.InProgress)
    {
        var task = project.WithTasks(1, (faker, _) => faker
            .WithStatus(status)
            .WithPlannedDateRange(end is { } e ? new FlexibleDateRange(e.PlusDays(-5), e) : null))[0];
        task.WithAssignees(assignee);
        _dbContext.AddProjectTask(task);
        return task;
    }

    [Fact]
    public async Task Handle_ReturnsOnlyTasksAssignedToTheCaller_EvenOnProjectsTheyLead()
    {
        // Arrange
        var led = AddProject(roles: new() { [ProjectRole.Manager] = [_employeeId] });
        var mine = AddTask(led, Today, _employeeId);
        AddTask(led, Today, _otherEmployeeId);

        // Act
        var result = await _handler.Handle(new GetMyProjectTasksQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(t => t.Id).Should().Equal(mine.Id);
        result[0].Project.Id.Should().Be(led.Id);
        result[0].Project.Key.Should().Be(led.Key.Value);
        result[0].DueDate.Should().Be(Today);
    }

    [Theory]
    [InlineData(TaskStatus.Completed)]
    [InlineData(TaskStatus.Canceled)]
    public async Task Handle_LeavesOutClosedTasks(TaskStatus closed)
    {
        // Arrange
        var project = AddProject();
        var open = AddTask(project, Today, _employeeId, TaskStatus.NotStarted);
        AddTask(project, Today, _employeeId, closed);

        // Act
        var result = await _handler.Handle(new GetMyProjectTasksQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(t => t.Id).Should().Equal(open.Id);
    }

    [Fact]
    public async Task Handle_WithAStatusFilter_KeepsOnlyTasksOnProjectsInThoseStatuses()
    {
        // Arrange
        var active = AddProject(ProjectStatus.Active);
        var approved = AddProject(ProjectStatus.Approved);
        var proposed = AddProject(ProjectStatus.Proposed);
        var onActive = AddTask(active, Today, _employeeId);
        var onApproved = AddTask(approved, Today.PlusDays(1), _employeeId);
        AddTask(proposed, Today, _employeeId);

        // Act
        var result = await _handler.Handle(
            new GetMyProjectTasksQuery([ProjectStatus.Approved, ProjectStatus.Active]),
            TestContext.Current.CancellationToken);

        // Assert
        result.Select(t => t.Id).Should().Equal(onActive.Id, onApproved.Id);
    }

    [Fact]
    public async Task Handle_WithoutAStatusFilter_KeepsOnlyTasksOnApprovedAndActiveProjects()
    {
        // Arrange
        var active = AddProject(ProjectStatus.Active);
        var approved = AddProject(ProjectStatus.Approved);
        var onActive = AddTask(active, Today, _employeeId);
        var onApproved = AddTask(approved, Today.PlusDays(1), _employeeId);
        foreach (var closed in new[] { ProjectStatus.Proposed, ProjectStatus.Completed, ProjectStatus.Canceled })
            AddTask(AddProject(closed), Today.PlusDays(-30), _employeeId);

        // Act
        var result = await _handler.Handle(new GetMyProjectTasksQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(t => t.Id).Should().Equal(onActive.Id, onApproved.Id);
    }

    [Fact]
    public async Task Handle_WithAStatusFilter_CanReachTasksOnAClosedProject()
    {
        // Arrange
        var completed = AddProject(ProjectStatus.Completed);
        var leftOpen = AddTask(completed, Today, _employeeId);
        AddTask(AddProject(ProjectStatus.Active), Today, _employeeId);

        // Act
        var result = await _handler.Handle(new GetMyProjectTasksQuery([ProjectStatus.Completed]), TestContext.Current.CancellationToken);

        // Assert
        result.Select(t => t.Id).Should().Equal(leftOpen.Id);
    }

    [Fact]
    public async Task Handle_OnTheSameDayAndProject_OrdersByTaskNumber()
    {
        // Arrange
        var project = AddProject();
        var tasks = project.WithTasks(10, (faker, _) => faker
            .WithStatus(TaskStatus.NotStarted)
            .WithPlannedDateRange(new FlexibleDateRange(Today.PlusDays(-1), Today)));
        foreach (var task in tasks)
            task.WithAssignees(_employeeId);
        // Added in reverse, so the order has to come from the number rather than from insertion.
        _dbContext.AddProjectTasks(Enumerable.Reverse(tasks));

        // Act
        var result = await _handler.Handle(new GetMyProjectTasksQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(t => t.Id).Should().Equal(tasks.Select(t => t.Id));
    }

    [Fact]
    public async Task Handle_OrdersOverdueFirst_ThenByDueDate_WithUndatedLast()
    {
        // Arrange
        var alpha = AddProject(name: "Alpha");
        var beta = AddProject(name: "Beta");
        var nextWeek = AddTask(alpha, Today.PlusDays(8), _employeeId);
        var undated = AddTask(alpha, null, _employeeId);
        var dueToday = AddTask(beta, Today, _employeeId);
        var longOverdue = AddTask(beta, Today.PlusDays(-10), _employeeId);
        var overdue = AddTask(alpha, Today.PlusDays(-1), _employeeId);
        var dueTodayOnAlpha = AddTask(alpha, Today, _employeeId);

        // Act
        var result = await _handler.Handle(new GetMyProjectTasksQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(t => t.Id).Should().Equal(
            longOverdue.Id, overdue.Id, dueTodayOnAlpha.Id, dueToday.Id, nextWeek.Id, undated.Id);
    }

    [Fact]
    public async Task Handle_ForAMilestone_DueDateIsItsPlannedDate()
    {
        // Arrange
        var project = AddProject();
        var milestone = project.WithTasks(1, (faker, _) => faker
            .WithType(ProjectTaskType.Milestone)
            .WithStatus(TaskStatus.NotStarted)
            .WithPlannedDateRange(null)
            .WithPlannedDate(Today.PlusDays(3)))[0];
        milestone.WithAssignees(_employeeId);
        _dbContext.AddProjectTask(milestone);

        // Act
        var result = await _handler.Handle(new GetMyProjectTasksQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.DueDate.Should().Be(Today.PlusDays(3));
    }

    [Fact]
    public async Task Handle_WhenTheAccountIsNotLinked_ReturnsNothing()
    {
        // Arrange
        AddTask(AddProject(), Today, _employeeId);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var result = await _handler.Handle(new GetMyProjectTasksQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }
}
