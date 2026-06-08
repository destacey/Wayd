using FluentAssertions;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared.Extensions;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Queries;

public class GetProjectTeamQueryHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly GetProjectTeamQueryHandler _handler;
    private readonly ProjectFaker _projectFaker;
    private readonly EmployeeFaker _employeeFaker;

    public GetProjectTeamQueryHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _handler = new GetProjectTeamQueryHandler(_dbContext);
        _projectFaker = new ProjectFaker();
        _employeeFaker = new EmployeeFaker();
    }

    private static HashSet<RoleAssignment<TaskRole>> CreateTaskAssigneeRoles(Guid taskId, Employee employee)
    {
        var role = new RoleAssignmentFaker<TaskRole>()
            .WithObjectId(taskId)
            .WithRole(TaskRole.Assignee)
            .WithEmployeeId(employee.Id)
            .Generate();
        role.Employee = employee;
        return [role];
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenProjectDoesNotExist()
    {
        // Arrange
        var query = new GetProjectTeamQuery("NONEXISTENT");

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenProjectHasNoTeamMembers()
    {
        // Arrange
        var projectKey = new ProjectKey("EMPTY");
        var project = _projectFaker.WithKey(projectKey).Generate();
        _dbContext.AddProject(project);

        var query = new GetProjectTeamQuery(projectKey.Value);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnMembersWithProjectRoles()
    {
        // Arrange
        var sponsor = _employeeFaker.Generate();
        var manager = _employeeFaker.Generate();

        var projectKey = new ProjectKey("ROLES");
        var project = _projectFaker.WithKey(projectKey).WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
            {
                { ProjectRole.Sponsor, [sponsor.Id] },
                { ProjectRole.Manager, [manager.Id] },
            }).Generate();

        foreach (var role in project.Roles)
        {
            if (role.EmployeeId == sponsor.Id) role.Employee = sponsor;
            else if (role.EmployeeId == manager.Id) role.Employee = manager;
        }

        _dbContext.AddProject(project);

        var query = new GetProjectTeamQuery(projectKey.Value);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);

        var sponsorMember = result!.Single(m => m.Employee.Id == sponsor.Id);
        sponsorMember.Roles.Should().ContainSingle().Which.Should().Be("Project Sponsor");
        sponsorMember.ActiveWorkItemCount.Should().Be(0);

        var managerMember = result!.Single(m => m.Employee.Id == manager.Id);
        managerMember.Roles.Should().ContainSingle().Which.Should().Be("Project Manager");
    }

    [Fact]
    public async Task Handle_ShouldConsolidateMultipleRolesPerPerson()
    {
        // Arrange
        var employee = _employeeFaker.Generate();

        var projectKey = new ProjectKey("MULTI");
        var project = _projectFaker.WithKey(projectKey).WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
            {
                { ProjectRole.Owner, [employee.Id] },
                { ProjectRole.Manager, [employee.Id] },
            }).Generate();

        foreach (var role in project.Roles)
            role.Employee = employee;

        _dbContext.AddProject(project);

        var query = new GetProjectTeamQuery(projectKey.Value);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].Roles.Should().HaveCount(2);
        result[0].Roles.Should().Contain("Project Owner");
        result[0].Roles.Should().Contain("Project Manager");
    }

    [Fact]
    public async Task Handle_ShouldIncludeTaskOnlyAssignees()
    {
        // Arrange
        var projectRoleMember = _employeeFaker.Generate();
        var taskOnlyAssignee = _employeeFaker.Generate();

        var projectKey = new ProjectKey("TASKS");
        var project = _projectFaker.WithKey(projectKey).WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
            {
                { ProjectRole.Manager, [projectRoleMember.Id] },
            }).Generate();

        foreach (var role in project.Roles)
            role.Employee = projectRoleMember;

        // Create a task assigned to taskOnlyAssignee (not in any project role)
        var task = new ProjectTaskFaker().WithProjectId(project.Id).WithStatus(TaskStatus.InProgress).Generate();
        task.SetPrivateField("_roles", CreateTaskAssigneeRoles(task.Id, taskOnlyAssignee));

        _dbContext.AddProject(project);
        _dbContext.AddProjectTask(task);

        var query = new GetProjectTeamQuery(projectKey.Value);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);

        var taskMember = result!.Single(m => m.Employee.Id == taskOnlyAssignee.Id);
        taskMember.Roles.Should().BeEmpty();
        taskMember.ActiveWorkItemCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldCountActiveLeafTasksOnly()
    {
        // Arrange
        var employee = _employeeFaker.Generate();

        var projectKey = new ProjectKey("COUNT");
        var project = _projectFaker.WithKey(projectKey).WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
            {
                { ProjectRole.Member, [employee.Id] },
            }).Generate();

        foreach (var role in project.Roles)
            role.Employee = employee;

        // Active leaf task (InProgress) - should be counted
        var activeTask = new ProjectTaskFaker().WithProjectId(project.Id).WithStatus(TaskStatus.InProgress).Generate();
        activeTask.SetPrivateField("_roles", CreateTaskAssigneeRoles(activeTask.Id, employee));

        // Active leaf task (NotStarted) - should be counted
        var notStartedTask = new ProjectTaskFaker().WithProjectId(project.Id).WithStatus(TaskStatus.NotStarted).Generate();
        notStartedTask.SetPrivateField("_roles", CreateTaskAssigneeRoles(notStartedTask.Id, employee));

        // Completed leaf task - should NOT be counted
        var completedTask = new ProjectTaskFaker().WithProjectId(project.Id).WithStatus(TaskStatus.Completed).Generate();
        completedTask.SetPrivateField("_roles", CreateTaskAssigneeRoles(completedTask.Id, employee));

        // Parent task with child (not a leaf) - should NOT be counted
        var parentTask = new ProjectTaskFaker().WithProjectId(project.Id).WithStatus(TaskStatus.InProgress).Generate();
        parentTask.SetPrivateField("_roles", CreateTaskAssigneeRoles(parentTask.Id, employee));

        var childTask = new ProjectTaskFaker().WithProjectId(project.Id).WithParentId(parentTask.Id).WithStatus(TaskStatus.InProgress).Generate();
        childTask.SetPrivateField("_roles", CreateTaskAssigneeRoles(childTask.Id, employee));

        _dbContext.AddProject(project);
        _dbContext.AddProjectTasks([activeTask, notStartedTask, completedTask, parentTask, childTask]);

        var query = new GetProjectTeamQuery(projectKey.Value);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        var member = result!.Single(m => m.Employee.Id == employee.Id);
        // Active leaf tasks: activeTask (InProgress), notStartedTask (NotStarted), childTask (InProgress leaf)
        // Excluded: completedTask (Completed), parentTask (has child, not leaf)
        member.ActiveWorkItemCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ShouldReturnResultsOrderedByName()
    {
        // Arrange
        var alice = _employeeFaker.WithName(new PersonName("Alice", null, "Adams")).Generate();
        var charlie = _employeeFaker.WithName(new PersonName("Charlie", null, "Chase")).Generate();
        var bob = _employeeFaker.WithName(new PersonName("Bob", null, "Baker")).Generate();

        var projectKey = new ProjectKey("ORDER");
        var project = _projectFaker.WithKey(projectKey).WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
            {
                { ProjectRole.Member, [alice.Id, charlie.Id, bob.Id] },
            }).Generate();

        foreach (var role in project.Roles)
        {
            if (role.EmployeeId == alice.Id) role.Employee = alice;
            else if (role.EmployeeId == charlie.Id) role.Employee = charlie;
            else if (role.EmployeeId == bob.Id) role.Employee = bob;
        }

        _dbContext.AddProject(project);

        var query = new GetProjectTeamQuery(projectKey.Value);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result![0].Employee.Name.Should().Be("Alice Adams");
        result[1].Employee.Name.Should().Be("Bob Baker");
        result[2].Employee.Name.Should().Be("Charlie Chase");
    }

    [Fact]
    public async Task Handle_ShouldWorkWithProjectId()
    {
        // Arrange
        var employee = _employeeFaker.Generate();
        var project = _projectFaker.WithRoles(new Dictionary<ProjectRole, HashSet<Guid>>
            {
                { ProjectRole.Sponsor, [employee.Id] },
            }).Generate();

        foreach (var role in project.Roles)
            role.Employee = employee;

        _dbContext.AddProject(project);

        var query = new GetProjectTeamQuery(project.Id.ToString());

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}