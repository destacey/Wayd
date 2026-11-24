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

public class AddTeamMemberCommandHandlerTests : IDisposable
{
    private readonly TeamFaker _teamFaker;
    private readonly EmployeeFaker _employeeFaker;
    private readonly FakeOrganizationDbContext _dbContext;
    private readonly AddTeamMemberCommandHandler _handler;
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 6, 2, 0, 0)));

    public AddTeamMemberCommandHandlerTests()
    {
        _teamFaker = new TeamFaker();
        _employeeFaker = new EmployeeFaker();
        _dbContext = new FakeOrganizationDbContext();

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);

        _handler = new AddTeamMemberCommandHandler(
            _dbContext,
            _dbContext,
            _dateTimeProvider,
            currentUser.Object,
            new Mock<ILogger<AddTeamMemberCommandHandler>>().Object);
    }

    [Fact]
    public async Task Handle_ShouldAddMember_WithSingleRole()
    {
        // Arrange
        var team = _teamFaker.Generate();
        var employee = _employeeFaker.Generate();
        var roleId = AddRole();
        _dbContext.AddTeam(team);
        _dbContext.AddEmployee(employee);

        var command = new AddTeamMemberCommand(team.Id, employee.Id, [roleId]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.Members.Should().HaveCount(1);
        team.Members.Single().EmployeeId.Should().Be(employee.Id);
        team.Members.Single().RoleId.Should().Be(roleId);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldAddMember_WithMultipleRoles()
    {
        // Arrange
        var team = _teamFaker.Generate();
        var employee = _employeeFaker.Generate();
        var roleId1 = AddRole();
        var roleId2 = AddRole();
        _dbContext.AddTeam(team);
        _dbContext.AddEmployee(employee);

        var command = new AddTeamMemberCommand(team.Id, employee.Id, [roleId1, roleId2]);

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
    public async Task Handle_ShouldFail_WhenEmployeeAlreadyOnTeamInSameRole()
    {
        // Arrange
        var team = _teamFaker.Generate();
        var employee = _employeeFaker.Generate();
        var roleId = AddRole();
        _dbContext.AddTeam(team);
        _dbContext.AddEmployee(employee);

        team.AddMember(employee, [roleId], EventActor.System, _dateTimeProvider.Now);

        var command = new AddTeamMemberCommand(team.Id, employee.Id, [roleId]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("same role");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTeamNotFound()
    {
        // Arrange
        var employee = _employeeFaker.Generate();
        _dbContext.AddEmployee(employee);

        var command = new AddTeamMemberCommand(Guid.NewGuid(), employee.Id, [Guid.NewGuid()]);

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

        var command = new AddTeamMemberCommand(team.Id, Guid.NewGuid(), [Guid.NewGuid()]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTeamIsInactive()
    {
        // Arrange
        var team = _teamFaker.AsInactive().Generate();
        var employee = _employeeFaker.Generate();
        _dbContext.AddTeam(team);
        _dbContext.AddEmployee(employee);

        var command = new AddTeamMemberCommand(team.Id, employee.Id, [AddRole()]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("inactive");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenEmployeeIsInactive()
    {
        // Arrange
        var team = _teamFaker.Generate();
        var employee = _employeeFaker.AsInactive().Generate();
        _dbContext.AddTeam(team);
        _dbContext.AddEmployee(employee);

        var command = new AddTeamMemberCommand(team.Id, employee.Id, [AddRole()]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("inactive");
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
        var unknownRoleId = Guid.NewGuid();
        var memberCount = team.Members.Count;

        // Act
        var result = await _handler.Handle(new AddTeamMemberCommand(team.Id, employee.Id, [AddRole(), unknownRoleId]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"Team member role {unknownRoleId} not found.");
        team.Members.Should().HaveCount(memberCount);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenARoleIsInactive()
    {
        // Arrange
        var team = _teamFaker.Generate();
        var employee = _employeeFaker.Generate();
        _dbContext.AddTeam(team);
        _dbContext.AddEmployee(employee);
        var inactiveRole = new TeamMemberRoleFaker().WithName("Archivist").AsInactive().Generate();
        _dbContext.AddTeamMemberRole(inactiveRole);

        // Act
        var result = await _handler.Handle(new AddTeamMemberCommand(team.Id, employee.Id, [AddRole(), inactiveRole.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Inactive roles cannot be assigned to team members. Archivist is inactive.");
        team.Members.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldReportTheRoleAsInactive_WhenTheMemberAlreadyHoldsIt()
    {
        // Arrange — the inactive check runs before the domain's duplicate check, so the caller learns the
        // role cannot be given at all rather than that it is already held
        var team = _teamFaker.Generate();
        var employee = _employeeFaker.Generate();
        _dbContext.AddTeam(team);
        _dbContext.AddEmployee(employee);
        var role = new TeamMemberRoleFaker().WithName("Archivist").Generate();
        _dbContext.AddTeamMemberRole(role);
        team.AddMember(employee, [role.Id], EventActor.System, _dateTimeProvider.Now);
        role.Deactivate();

        // Act
        var result = await _handler.Handle(new AddTeamMemberCommand(team.Id, employee.Id, [role.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Inactive roles cannot be assigned to team members. Archivist is inactive.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    /// <summary>Adds a team member role to the context and returns its id.</summary>
    private Guid AddRole()
    {
        var role = new TeamMemberRoleFaker().Generate();
        _dbContext.AddTeamMemberRole(role);
        return role.Id;
    }
}
