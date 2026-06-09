using FluentAssertions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Queries;

public class GetMyProjectsTaskMetricsQueryHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly GetMyProjectsTaskMetricsQueryHandler _handler;
    private readonly Guid _employeeId = Guid.NewGuid();
    private readonly Guid _otherEmployeeId = Guid.NewGuid();

    // Use a Wednesday so we have clear week boundaries
    private static readonly LocalDate Today = new(2026, 3, 18); // Wednesday

    public GetMyProjectsTaskMetricsQueryHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _currentUserMock = new Mock<ICurrentUser>();
        _currentUserMock.Setup(u => u.GetEmployeeId()).Returns(_employeeId);

        var instant = Today.AtStartOfDayInZone(DateTimeZone.Utc).ToInstant();
        var clock = new FakeClock(instant);
        _dateTimeProvider = new TestingDateTimeProvider(clock);

        _handler = new GetMyProjectsTaskMetricsQueryHandler(_dbContext, _currentUserMock.Object, _dateTimeProvider);
    }

    #region Helpers

    private FlexibleDateRange OverdueDateRange()
    {
        return new FlexibleDateRange(Today.PlusDays(-10), Today.PlusDays(-1));
    }

    private FlexibleDateRange DueThisWeekDateRange()
    {
        var daysUntilSaturday = ((int)IsoDayOfWeek.Saturday - (int)Today.DayOfWeek + 7) % 7;
        return new FlexibleDateRange(Today.PlusDays(-3), Today.PlusDays(daysUntilSaturday));
    }

    private FlexibleDateRange UpcomingDateRange()
    {
        var daysUntilSaturday = ((int)IsoDayOfWeek.Saturday - (int)Today.DayOfWeek + 7) % 7;
        return new FlexibleDateRange(Today, Today.PlusDays(daysUntilSaturday + 3));
    }

    #endregion

    [Fact]
    public async Task Handle_LeadershipRole_ShouldCountAllTasksOnProject()
    {
        // Arrange: user is PM on a project with 2 overdue tasks — one assigned to user, one to someone else
        var project = new ProjectFaker().WithStatus(ProjectStatus.Active).WithRoles(new Dictionary<ProjectRole, HashSet<Guid>> { { ProjectRole.Manager, [_employeeId] } }).Generate();

        var overdueDates = OverdueDateRange();
        var tasks = project.WithTasks(2, (faker, i) =>
        {
            faker.WithStatus(TaskStatus.InProgress).WithPlannedDateRange(overdueDates);
        });
        tasks[0].WithAssignees(_employeeId);
        tasks[1].WithAssignees(_otherEmployeeId);

        _dbContext.AddProject(project);
        _dbContext.AddProjectTasks(tasks);

        // Act
        var result = await _handler.Handle(new GetMyProjectsTaskMetricsQuery(), TestContext.Current.CancellationToken);

        // Assert: PM should see ALL overdue tasks on the project, not just their own
        result.Overdue.Should().Be(2);
    }

    [Fact]
    public async Task Handle_AssigneeOnly_ShouldCountOnlyAssignedTasks()
    {
        // Arrange: user has NO project role, but is assigned to 1 of 3 overdue tasks
        var project = new ProjectFaker().WithStatus(ProjectStatus.Active).Generate();

        var overdueDates = OverdueDateRange();
        var tasks = project.WithTasks(3, (faker, _) =>
        {
            faker.WithStatus(TaskStatus.InProgress).WithPlannedDateRange(overdueDates);
        });
        tasks[0].WithAssignees(_employeeId);
        tasks[1].WithAssignees(_otherEmployeeId);
        tasks[2].WithAssignees(_otherEmployeeId);

        _dbContext.AddProject(project);
        _dbContext.AddProjectTasks(tasks);

        // Act
        var result = await _handler.Handle(new GetMyProjectsTaskMetricsQuery(), TestContext.Current.CancellationToken);

        // Assert: user is only a task assignee (no project role), should only see their 1 task
        result.Overdue.Should().Be(1);
    }

    [Fact]
    public async Task Handle_LeadershipAndAssignee_ShouldNotDoubleCount()
    {
        // Arrange: user is PM AND assigned to a task on the same project
        var project = new ProjectFaker().WithStatus(ProjectStatus.Active).WithRoles(new Dictionary<ProjectRole, HashSet<Guid>> { { ProjectRole.Manager, [_employeeId] } }).Generate();

        var overdueDates = OverdueDateRange();
        var tasks = project.WithTasks(2, (faker, _) =>
        {
            faker.WithStatus(TaskStatus.InProgress).WithPlannedDateRange(overdueDates);
        });
        tasks[0].WithAssignees(_employeeId);
        tasks[1].WithAssignees(_otherEmployeeId);

        _dbContext.AddProject(project);
        _dbContext.AddProjectTasks(tasks);

        // Act
        var result = await _handler.Handle(new GetMyProjectsTaskMetricsQuery(), TestContext.Current.CancellationToken);

        // Assert: PM sees all tasks, the task they're assigned to should not be double-counted
        result.Overdue.Should().Be(2);
    }

    [Fact]
    public async Task Handle_MixedRoles_LeadershipOnOneProject_AssigneeOnAnother()
    {
        // Arrange:
        // Project A: user is PM — has 3 overdue tasks (2 assigned to others, 1 to user)
        var projectA = new ProjectFaker().WithStatus(ProjectStatus.Active).WithRoles(new Dictionary<ProjectRole, HashSet<Guid>> { { ProjectRole.Manager, [_employeeId] } }).Generate();

        var overdueDates = OverdueDateRange();
        var tasksA = projectA.WithTasks(3, (faker, _) =>
        {
            faker.WithStatus(TaskStatus.InProgress).WithPlannedDateRange(overdueDates);
        });
        tasksA[0].WithAssignees(_employeeId);
        tasksA[1].WithAssignees(_otherEmployeeId);
        tasksA[2].WithAssignees(_otherEmployeeId);

        _dbContext.AddProject(projectA);
        _dbContext.AddProjectTasks(tasksA);

        // Project B: user is only a task assignee — has 3 overdue tasks (1 assigned to user, 2 to others)
        var projectB = new ProjectFaker().WithStatus(ProjectStatus.Active).Generate();

        var tasksB = projectB.WithTasks(3, (faker, _) =>
        {
            faker.WithStatus(TaskStatus.InProgress).WithPlannedDateRange(overdueDates);
        });
        tasksB[0].WithAssignees(_employeeId);
        tasksB[1].WithAssignees(_otherEmployeeId);
        tasksB[2].WithAssignees(_otherEmployeeId);

        _dbContext.AddProject(projectB);
        _dbContext.AddProjectTasks(tasksB);

        // Act
        var result = await _handler.Handle(new GetMyProjectsTaskMetricsQuery(), TestContext.Current.CancellationToken);

        // Assert:
        // Project A (PM): all 3 tasks counted
        // Project B (assignee only): only 1 task counted (the one assigned to user)
        // Total: 4
        result.Overdue.Should().Be(4);
    }

    [Fact]
    public async Task Handle_MemberRole_ShouldCountOnlyAssignedTasks()
    {
        // Arrange: user is a Member on a project — should only see their assigned tasks
        var project = new ProjectFaker().WithStatus(ProjectStatus.Active).WithRoles(new Dictionary<ProjectRole, HashSet<Guid>> { { ProjectRole.Member, [_employeeId] } }).Generate();

        var overdueDates = OverdueDateRange();
        var tasks = project.WithTasks(2, (faker, _) =>
        {
            faker.WithStatus(TaskStatus.InProgress).WithPlannedDateRange(overdueDates);
        });
        tasks[0].WithAssignees(_employeeId);
        tasks[1].WithAssignees(_otherEmployeeId);

        _dbContext.AddProject(project);
        _dbContext.AddProjectTasks(tasks);

        // Act
        var result = await _handler.Handle(new GetMyProjectsTaskMetricsQuery(), TestContext.Current.CancellationToken);

        // Assert: Member should only see their assigned tasks, not all tasks on the project
        result.Overdue.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SponsorRole_ShouldCountAllTasks()
    {
        // Arrange: user is Sponsor — leadership role, should see all tasks
        var project = new ProjectFaker().WithStatus(ProjectStatus.Active).WithRoles(new Dictionary<ProjectRole, HashSet<Guid>> { { ProjectRole.Sponsor, [_employeeId] } }).Generate();

        var tasks = project.WithTasks(2, (faker, i) =>
        {
            faker.WithStatus(TaskStatus.InProgress).WithPlannedDateRange(i == 1 ? OverdueDateRange() : DueThisWeekDateRange());
        });
        tasks[0].WithAssignees(_otherEmployeeId);
        tasks[1].WithAssignees(_otherEmployeeId);

        _dbContext.AddProject(project);
        _dbContext.AddProjectTasks(tasks);

        // Act
        var result = await _handler.Handle(new GetMyProjectsTaskMetricsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Overdue.Should().Be(1);
        result.DueThisWeek.Should().Be(1);
    }

    [Fact]
    public async Task Handle_OwnerRole_ShouldCountAllTasks()
    {
        // Arrange: user is Owner — leadership role
        var project = new ProjectFaker().WithStatus(ProjectStatus.Active).WithRoles(new Dictionary<ProjectRole, HashSet<Guid>> { { ProjectRole.Owner, [_employeeId] } }).Generate();

        var tasks = project.WithTasks(1, (faker, _) =>
        {
            faker.WithStatus(TaskStatus.NotStarted).WithPlannedDateRange(UpcomingDateRange());
        });
        tasks[0].WithAssignees(_otherEmployeeId);

        _dbContext.AddProject(project);
        _dbContext.AddProjectTasks(tasks);

        // Act
        var result = await _handler.Handle(new GetMyProjectsTaskMetricsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Upcoming.Should().Be(1);
    }

    [Fact]
    public async Task Handle_FilteredToAssigneeOnly_ShouldIgnoreLeadershipRole()
    {
        // Arrange: user is Owner on a project, but filtered to Task Assignee only
        // Should only count tasks assigned to user, not all tasks
        var project = new ProjectFaker().WithStatus(ProjectStatus.Active).WithRoles(new Dictionary<ProjectRole, HashSet<Guid>> { { ProjectRole.Owner, [_employeeId] } }).Generate();

        var overdueDates = OverdueDateRange();
        var tasks = project.WithTasks(3, (faker, _) =>
        {
            faker.WithStatus(TaskStatus.InProgress).WithPlannedDateRange(overdueDates);
        });
        tasks[0].WithAssignees(_employeeId);
        tasks[1].WithAssignees(_otherEmployeeId);
        tasks[2].WithAssignees(_otherEmployeeId);

        _dbContext.AddProject(project);
        _dbContext.AddProjectTasks(tasks);

        // Act — filtered to Task Assignee role only
        var result = await _handler.Handle(
            new GetMyProjectsTaskMetricsQuery(RoleFilter: [ProjectMemberRole.Assignee]),
            TestContext.Current.CancellationToken);

        // Assert: even though user is Owner, filter says Assignee only — count only assigned tasks
        result.Overdue.Should().Be(1);
    }

    [Fact]
    public async Task Handle_FilteredToOwnerAndAssignee_ShouldUseLeadershipForOwnerProjects()
    {
        // Arrange: user is Owner on a project, filtered to Owner + Task Assignee
        // Owner is a leadership role, so all tasks should be visible
        var project = new ProjectFaker().WithStatus(ProjectStatus.Active).WithRoles(new Dictionary<ProjectRole, HashSet<Guid>> { { ProjectRole.Owner, [_employeeId] } }).Generate();

        var overdueDates = OverdueDateRange();
        var tasks = project.WithTasks(3, (faker, _) =>
        {
            faker.WithStatus(TaskStatus.InProgress).WithPlannedDateRange(overdueDates);
        });
        tasks[0].WithAssignees(_employeeId);
        tasks[1].WithAssignees(_otherEmployeeId);
        tasks[2].WithAssignees(_otherEmployeeId);

        _dbContext.AddProject(project);
        _dbContext.AddProjectTasks(tasks);

        // Act — filtered to Owner + Task Assignee
        var result = await _handler.Handle(
            new GetMyProjectsTaskMetricsQuery(RoleFilter: [ProjectMemberRole.Owner, ProjectMemberRole.Assignee]),
            TestContext.Current.CancellationToken);

        // Assert: Owner is leadership — all tasks counted
        result.Overdue.Should().Be(3);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}