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

public class GetProjectsPlanSummariesQueryHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly GetProjectsPlanSummariesQueryHandler _handler;
    private readonly Guid _employeeId = Guid.NewGuid();
    private readonly Guid _otherEmployeeId = Guid.NewGuid();

    // Use a Wednesday so we have clear week boundaries
    private static readonly LocalDate Today = new(2026, 3, 18); // Wednesday

    public GetProjectsPlanSummariesQueryHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _currentUserMock = new Mock<ICurrentUser>();
        _currentUserMock.Setup(u => u.GetEmployeeId()).Returns(_employeeId);

        var instant = Today.AtStartOfDayInZone(DateTimeZone.Utc).ToInstant();
        var clock = new FakeClock(instant);
        _dateTimeProvider = new TestingDateTimeProvider(clock);

        _handler = new GetProjectsPlanSummariesQueryHandler(_dbContext, _currentUserMock.Object, _dateTimeProvider);
    }

    #region Helpers

    private static FlexibleDateRange OverdueDateRange()
    {
        return new FlexibleDateRange(Today.PlusDays(-10), Today.PlusDays(-1));
    }

    private static FlexibleDateRange DueThisWeekDateRange()
    {
        var daysUntilSaturday = ((int)IsoDayOfWeek.Saturday - (int)Today.DayOfWeek + 7) % 7;
        return new FlexibleDateRange(Today.PlusDays(-3), Today.PlusDays(daysUntilSaturday));
    }

    private static FlexibleDateRange UpcomingDateRange()
    {
        var daysUntilSaturday = ((int)IsoDayOfWeek.Saturday - (int)Today.DayOfWeek + 7) % 7;
        return new FlexibleDateRange(Today, Today.PlusDays(daysUntilSaturday + 3));
    }

    #endregion

    [Fact]
    public async Task Handle_EmptyProjectIds_ShouldReturnEmptyDictionary()
    {
        var result = await _handler.Handle(
            new GetProjectsPlanSummariesQuery([]),
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NoEmployeeId_ShouldReturnEmptyDictionary()
    {
        _currentUserMock.Setup(u => u.GetEmployeeId()).Returns((Guid?)null);

        var project = new ProjectFaker().WithStatus(ProjectStatus.Active).Generate();
        _dbContext.AddProject(project);

        var result = await _handler.Handle(
            new GetProjectsPlanSummariesQuery([project.Id]),
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_LeadershipRole_ShouldCountAllTasksOnProject()
    {
        // Arrange: user is PM on a project with 2 overdue tasks — one assigned to user, one to someone else
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
        var result = await _handler.Handle(
            new GetProjectsPlanSummariesQuery([project.Id]),
            TestContext.Current.CancellationToken);

        // Assert: PM should see ALL overdue tasks on the project
        result.Should().ContainKey(project.Id);
        result[project.Id].Overdue.Should().Be(2);
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
        var result = await _handler.Handle(
            new GetProjectsPlanSummariesQuery([project.Id]),
            TestContext.Current.CancellationToken);

        // Assert: user is only a task assignee, should only see their 1 task
        result.Should().ContainKey(project.Id);
        result[project.Id].Overdue.Should().Be(1);
    }

    [Fact]
    public async Task Handle_MixedRoles_ShouldReturnPerProjectSummaries()
    {
        // Arrange:
        // Project A: user is PM — has 3 overdue tasks (all visible)
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

        // Project B: user is only a task assignee — has 3 overdue tasks (1 visible)
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
        var result = await _handler.Handle(
            new GetProjectsPlanSummariesQuery([projectA.Id, projectB.Id]),
            TestContext.Current.CancellationToken);

        // Assert: per-project results
        result.Should().ContainKey(projectA.Id);
        result[projectA.Id].Overdue.Should().Be(3);

        result.Should().ContainKey(projectB.Id);
        result[projectB.Id].Overdue.Should().Be(1);
    }

    [Fact]
    public async Task Handle_FilteredToAssigneeOnly_ShouldIgnoreLeadershipRole()
    {
        // Arrange: user is Owner on a project, but filtered to Task Assignee only
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
            new GetProjectsPlanSummariesQuery([project.Id], RoleFilter: [ProjectMemberRole.Assignee]),
            TestContext.Current.CancellationToken);

        // Assert: even though user is Owner, filter says Assignee only — count only assigned tasks
        result.Should().ContainKey(project.Id);
        result[project.Id].Overdue.Should().Be(1);
    }

    [Fact]
    public async Task Handle_NonSelectedLeadershipRole_ShouldScopeToAssignee()
    {
        // Arrange: user is Owner on a project, filtered to PM + Task Assignee
        // Owner is NOT in the selected leadership roles, so should scope to assignee
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

        // Act — filtered to PM + Task Assignee (user is Owner, not PM)
        var result = await _handler.Handle(
            new GetProjectsPlanSummariesQuery([project.Id], RoleFilter: [ProjectMemberRole.Manager, ProjectMemberRole.Assignee]),
            TestContext.Current.CancellationToken);

        // Assert: user is Owner but PM is the selected leadership role — Owner doesn't match,
        // so tasks are scoped to assignee only
        result.Should().ContainKey(project.Id);
        result[project.Id].Overdue.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SelectedLeadershipRole_ShouldCountAllTasks()
    {
        // Arrange: user is Owner on a project, filtered to Owner + Task Assignee
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
            new GetProjectsPlanSummariesQuery([project.Id], RoleFilter: [ProjectMemberRole.Owner, ProjectMemberRole.Assignee]),
            TestContext.Current.CancellationToken);

        // Assert: Owner is in the selected leadership roles — all tasks counted
        result.Should().ContainKey(project.Id);
        result[project.Id].Overdue.Should().Be(3);
    }

    [Fact]
    public async Task Handle_DateBuckets_ShouldCategorizeCorrectly()
    {
        // Arrange: user is Sponsor — leadership role, sees all tasks
        var project = new ProjectFaker().WithStatus(ProjectStatus.Active).WithRoles(new Dictionary<ProjectRole, HashSet<Guid>> { { ProjectRole.Sponsor, [_employeeId] } }).Generate();

        var tasks = project.WithTasks(3, (faker, i) =>
        {
            var dateRange = i switch
            {
                0 => OverdueDateRange(),
                1 => DueThisWeekDateRange(),
                2 => UpcomingDateRange(),
                _ => OverdueDateRange()
            };
            faker.WithStatus(TaskStatus.InProgress).WithPlannedDateRange(dateRange);
        });
        tasks[0].WithAssignees(_otherEmployeeId);
        tasks[1].WithAssignees(_otherEmployeeId);
        tasks[2].WithAssignees(_otherEmployeeId);

        _dbContext.AddProject(project);
        _dbContext.AddProjectTasks(tasks);

        // Act
        var result = await _handler.Handle(
            new GetProjectsPlanSummariesQuery([project.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainKey(project.Id);
        result[project.Id].Overdue.Should().Be(1);
        result[project.Id].DueThisWeek.Should().Be(1);
        result[project.Id].Upcoming.Should().Be(1);
    }

    [Fact]
    public async Task Handle_TotalLeafTasks_ShouldBeRoleScoped()
    {
        // Arrange: user is only a task assignee, assigned to 1 of 3 tasks
        var project = new ProjectFaker().WithStatus(ProjectStatus.Active).Generate();

        var tasks = project.WithTasks(3, (faker, _) =>
        {
            faker.WithStatus(TaskStatus.InProgress).WithPlannedDateRange(OverdueDateRange());
        });
        tasks[0].WithAssignees(_employeeId);
        tasks[1].WithAssignees(_otherEmployeeId);
        tasks[2].WithAssignees(_otherEmployeeId);

        _dbContext.AddProject(project);
        _dbContext.AddProjectTasks(tasks);

        // Act
        var result = await _handler.Handle(
            new GetProjectsPlanSummariesQuery([project.Id]),
            TestContext.Current.CancellationToken);

        // Assert: TotalLeafTasks should be scoped to visible tasks (1, not 3)
        result.Should().ContainKey(project.Id);
        result[project.Id].TotalLeafTasks.Should().Be(1);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}