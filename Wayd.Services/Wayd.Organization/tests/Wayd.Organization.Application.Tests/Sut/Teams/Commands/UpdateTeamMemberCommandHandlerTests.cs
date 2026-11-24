using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Identity;
using Wayd.Tests.Shared;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Tests.Data;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Commands;

public class UpdateTeamMemberCommandHandlerTests : IDisposable
{
    private readonly TeamFaker _teamFaker;
    private readonly EmployeeFaker _employeeFaker;
    private readonly FakeOrganizationDbContext _dbContext;
    private readonly UpdateTeamMemberCommandHandler _handler;
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 6, 2, 0, 0)));

    public UpdateTeamMemberCommandHandlerTests()
    {
        _teamFaker = new TeamFaker();
        _employeeFaker = new EmployeeFaker();
        _dbContext = new FakeOrganizationDbContext();

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);

        _handler = new UpdateTeamMemberCommandHandler(
            _dbContext,
            _dbContext,
            _dateTimeProvider,
            currentUser.Object,
            new Mock<ILogger<UpdateTeamMemberCommandHandler>>().Object);
    }

    [Fact]
    public async Task Handle_ShouldAddNewRole_WhenRoleNotAlreadyAssigned()
    {
        // Arrange
        var team = _teamFaker.Generate();
        var employee = _employeeFaker.Generate();
        var roleId1 = AddRole();
        var roleId2 = AddRole();
        _dbContext.AddTeam(team);
        _dbContext.AddEmployee(employee);

        team.AddMember(employee, [roleId1], EventActor.System, _dateTimeProvider.Now);

        var command = new UpdateTeamMemberCommand(team.Id, employee.Id, [roleId1, roleId2]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var activeMembers = team.Members.Where(m => !m.IsDeleted).ToList();
        activeMembers.Should().HaveCount(2);
        activeMembers.Select(m => m.RoleId).Should().BeEquivalentTo([roleId1, roleId2]);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRemoveRole_WhenRoleOmittedFromRequest()
    {
        // Arrange
        var team = _teamFaker.Generate();
        var employee = _employeeFaker.Generate();
        var roleId1 = AddRole();
        var roleId2 = AddRole();
        _dbContext.AddTeam(team);
        _dbContext.AddEmployee(employee);

        team.AddMember(employee, [roleId1], EventActor.System, _dateTimeProvider.Now);
        team.AddMember(employee, [roleId2], EventActor.System, _dateTimeProvider.Now);

        var command = new UpdateTeamMemberCommand(team.Id, employee.Id, [roleId1]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var activeMembers = team.Members.Where(m => !m.IsDeleted).ToList();
        activeMembers.Should().HaveCount(1);
        activeMembers.Single().RoleId.Should().Be(roleId1);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTeamNotFound()
    {
        // Arrange
        var command = new UpdateTeamMemberCommand(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenEmployeeNotFound()
    {
        // Arrange
        var team = _teamFaker.Generate();
        _dbContext.AddTeam(team);

        var command = new UpdateTeamMemberCommand(team.Id, Guid.NewGuid(), [Guid.NewGuid()]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenARoleDoesNotExist()
    {
        // Arrange
        var team = _teamFaker.Generate();
        var employee = _employeeFaker.Generate();
        _dbContext.AddTeam(team);
        _dbContext.AddEmployee(employee);
        team.AddMember(employee, [AddRole()], EventActor.System, _dateTimeProvider.Now);
        var unknownRoleId = Guid.NewGuid();
        var memberCount = team.Members.Count;

        // Act
        var result = await _handler.Handle(new UpdateTeamMemberCommand(team.Id, employee.Id, [AddRole(), unknownRoleId]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"Team member role {unknownRoleId} not found.");
        team.Members.Should().HaveCount(memberCount);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenANewRoleIsInactive()
    {
        // Arrange
        var team = _teamFaker.Generate();
        var employee = _employeeFaker.Generate();
        _dbContext.AddTeam(team);
        _dbContext.AddEmployee(employee);
        var heldRoleId = AddRole();
        team.AddMember(employee, [heldRoleId], EventActor.System, _dateTimeProvider.Now);
        var inactiveRole = new TeamMemberRoleFaker().WithName("Archivist").AsInactive().Generate();
        _dbContext.AddTeamMemberRole(inactiveRole);

        // Act
        var result = await _handler.Handle(new UpdateTeamMemberCommand(team.Id, employee.Id, [heldRoleId, inactiveRole.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Inactive roles cannot be assigned to team members. Archivist is inactive.");
        team.Members.Where(m => !m.IsDeleted).Select(m => m.RoleId).Should().BeEquivalentTo([heldRoleId]);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldKeepAHeldRole_WhenItHasSinceBeenDeactivated()
    {
        // Arrange — the member holds a role that was deactivated after it was given; an update that leaves
        // it in place while adding another is not refused
        var team = _teamFaker.Generate();
        var employee = _employeeFaker.Generate();
        _dbContext.AddTeam(team);
        _dbContext.AddEmployee(employee);
        var deactivatedRole = new TeamMemberRoleFaker().Generate();
        _dbContext.AddTeamMemberRole(deactivatedRole);
        team.AddMember(employee, [deactivatedRole.Id], EventActor.System, _dateTimeProvider.Now);
        deactivatedRole.Deactivate();
        var newRoleId = AddRole();

        // Act
        var result = await _handler.Handle(new UpdateTeamMemberCommand(team.Id, employee.Id, [deactivatedRole.Id, newRoleId]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.Members.Where(m => !m.IsDeleted).Select(m => m.RoleId).Should().BeEquivalentTo([deactivatedRole.Id, newRoleId]);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenRestoringARemovedRoleThatIsNowInactive()
    {
        // Arrange — the member once held the role, but that membership row is soft-deleted, so it is not held
        var team = _teamFaker.Generate();
        var employee = _employeeFaker.Generate();
        _dbContext.AddTeam(team);
        _dbContext.AddEmployee(employee);
        var keptRoleId = AddRole();
        var removedRole = new TeamMemberRoleFaker().WithName("Archivist").Generate();
        _dbContext.AddTeamMemberRole(removedRole);
        team.AddMember(employee, [keptRoleId, removedRole.Id], EventActor.System, _dateTimeProvider.Now);
        team.UpdateMemberRoles(employee, [keptRoleId], EventActor.System, _dateTimeProvider.Now);
        removedRole.Deactivate();

        // Act
        var result = await _handler.Handle(new UpdateTeamMemberCommand(team.Id, employee.Id, [keptRoleId, removedRole.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Inactive roles cannot be assigned to team members. Archivist is inactive.");
        team.Members.Where(m => !m.IsDeleted).Select(m => m.RoleId).Should().BeEquivalentTo([keptRoleId]);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldRemoveARole_WhenTheMemberAlsoHoldsADeactivatedRole()
    {
        // Arrange
        var team = _teamFaker.Generate();
        var employee = _employeeFaker.Generate();
        _dbContext.AddTeam(team);
        _dbContext.AddEmployee(employee);
        var deactivatedRole = new TeamMemberRoleFaker().Generate();
        _dbContext.AddTeamMemberRole(deactivatedRole);
        var droppedRoleId = AddRole();
        team.AddMember(employee, [deactivatedRole.Id, droppedRoleId], EventActor.System, _dateTimeProvider.Now);
        deactivatedRole.Deactivate();

        // Act
        var result = await _handler.Handle(new UpdateTeamMemberCommand(team.Id, employee.Id, [deactivatedRole.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.Members.Where(m => !m.IsDeleted).Select(m => m.RoleId).Should().BeEquivalentTo([deactivatedRole.Id]);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    /// <summary>Adds a team member role to the context and returns its id.</summary>
    private Guid AddRole()
    {
        var role = new TeamMemberRoleFaker().Generate();
        _dbContext.AddTeamMemberRole(role);
        return role.Id;
    }
}
