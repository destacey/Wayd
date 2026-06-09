using FluentAssertions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared.Extensions;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Queries;

public class GetMyProjectsSummaryQueryHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly GetMyProjectsSummaryQueryHandler _handler;
    private readonly Guid _employeeId = Guid.NewGuid();

    public GetMyProjectsSummaryQueryHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _currentUserMock = new Mock<ICurrentUser>();
        _currentUserMock.Setup(u => u.GetEmployeeId()).Returns(_employeeId);
        _handler = new GetMyProjectsSummaryQueryHandler(_dbContext, _currentUserMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptySummary_WhenNoEmployeeId()
    {
        // Arrange
        _currentUserMock.Setup(u => u.GetEmployeeId()).Returns((Guid?)null);
        var query = new GetMyProjectsSummaryQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldReturnZeroCounts_WhenNoProjects()
    {
        // Arrange
        var query = new GetMyProjectsSummaryQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.TotalCount.Should().Be(0);
        result.SponsorCount.Should().Be(0);
        result.OwnerCount.Should().Be(0);
        result.ManagerCount.Should().Be(0);
        result.MemberCount.Should().Be(0);
        result.AssigneeCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldCountSponsorRole()
    {
        // Arrange
        var project = new ProjectFaker().WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
            {
                { ProjectRole.Sponsor, [_employeeId] },
            }).Generate();
        _dbContext.AddProject(project);

        var query = new GetMyProjectsSummaryQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result!.SponsorCount.Should().Be(1);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldCountOwnerRole()
    {
        // Arrange
        var project = new ProjectFaker().WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
            {
                { ProjectRole.Owner, [_employeeId] },
            }).Generate();
        _dbContext.AddProject(project);

        var query = new GetMyProjectsSummaryQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result!.OwnerCount.Should().Be(1);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldCountManagerRole()
    {
        // Arrange
        var project = new ProjectFaker().WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
            {
                { ProjectRole.Manager, [_employeeId] },
            }).Generate();
        _dbContext.AddProject(project);

        var query = new GetMyProjectsSummaryQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result!.ManagerCount.Should().Be(1);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldCountMemberRole()
    {
        // Arrange
        var project = new ProjectFaker().WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
            {
                { ProjectRole.Member, [_employeeId] },
            }).Generate();
        _dbContext.AddProject(project);

        var query = new GetMyProjectsSummaryQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result!.MemberCount.Should().Be(1);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldCountTaskAssignee()
    {
        // Arrange
        var project = new ProjectFaker().Generate();

        var task = new ProjectTaskFaker().WithProjectId(project.Id).WithStatus(TaskStatus.InProgress).Generate().WithAssignees(_employeeId);
        project.AddToPrivateList("_tasks", task);

        _dbContext.AddProject(project);

        var query = new GetMyProjectsSummaryQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result!.AssigneeCount.Should().Be(1);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldCountMultipleRolesOnSameProject()
    {
        // Arrange — user is both Sponsor and Manager on same project
        var project = new ProjectFaker().WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
            {
                { ProjectRole.Sponsor, [_employeeId] },
                { ProjectRole.Manager, [_employeeId] },
            }).Generate();
        _dbContext.AddProject(project);

        var query = new GetMyProjectsSummaryQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result!.SponsorCount.Should().Be(1);
        result.ManagerCount.Should().Be(1);
        result.TotalCount.Should().Be(1); // same project counted once
    }

    [Fact]
    public async Task Handle_ShouldCountAcrossMultipleProjects()
    {
        // Arrange
        var project1 = new ProjectFaker().WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
            {
                { ProjectRole.Sponsor, [_employeeId] },
            }).Generate();

        var project2 = new ProjectFaker().WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
            {
                { ProjectRole.Manager, [_employeeId] },
            }).Generate();

        _dbContext.AddProjects([project1, project2]);

        var query = new GetMyProjectsSummaryQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result!.SponsorCount.Should().Be(1);
        result.ManagerCount.Should().Be(1);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldNotCountOtherUsersProjects()
    {
        // Arrange
        var otherId = Guid.NewGuid();
        var project = new ProjectFaker().WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
            {
                { ProjectRole.Sponsor, [otherId] },
            }).Generate();
        _dbContext.AddProject(project);

        var query = new GetMyProjectsSummaryQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result!.TotalCount.Should().Be(0);
        result.SponsorCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFilterByStatus()
    {
        // Arrange
        var activeProject = new ProjectFaker().WithStatus(ProjectStatus.Active).WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
            {
                { ProjectRole.Owner, [_employeeId] },
            }).Generate();

        var proposedProject = new ProjectFaker().WithStatus(ProjectStatus.Proposed).WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
            {
                { ProjectRole.Owner, [_employeeId] },
            }).Generate();

        _dbContext.AddProjects([activeProject, proposedProject]);

        var query = new GetMyProjectsSummaryQuery(StatusFilter: [ProjectStatus.Active]);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result!.TotalCount.Should().Be(1);
        result.OwnerCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldReturnAllStatuses_WhenNoStatusFilter()
    {
        // Arrange
        var activeProject = new ProjectFaker().WithStatus(ProjectStatus.Active).WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
            {
                { ProjectRole.Owner, [_employeeId] },
            }).Generate();

        var proposedProject = new ProjectFaker().WithStatus(ProjectStatus.Proposed).WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
            {
                { ProjectRole.Manager, [_employeeId] },
            }).Generate();

        _dbContext.AddProjects([activeProject, proposedProject]);

        var query = new GetMyProjectsSummaryQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result!.TotalCount.Should().Be(2);
        result.OwnerCount.Should().Be(1);
        result.ManagerCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldCountAssigneeWhoAlsoHasProjectRole()
    {
        // Arrange — user is a Manager AND has task assignments on same project
        var project = new ProjectFaker().WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
            {
                { ProjectRole.Manager, [_employeeId] },
            }).Generate();

        var task = new ProjectTaskFaker().WithProjectId(project.Id).WithStatus(TaskStatus.InProgress).Generate().WithAssignees(_employeeId);
        project.AddToPrivateList("_tasks", task);

        _dbContext.AddProject(project);

        var query = new GetMyProjectsSummaryQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result!.ManagerCount.Should().Be(1);
        result.AssigneeCount.Should().Be(1); // counted as assignee too
        result.TotalCount.Should().Be(1); // but total is still 1 project
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}