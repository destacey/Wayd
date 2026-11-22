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

    /// <summary>Adds a team member role to the context and returns its id.</summary>
    private Guid AddRole()
    {
        var role = new TeamMemberRoleFaker().Generate();
        _dbContext.AddTeamMemberRole(role);
        return role.Id;
    }
}
