using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.TestData;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Commands;

public class UpdateTeamMemberCommandHandlerTests : IDisposable
{
    private readonly TeamFaker _teamFaker;
    private readonly EmployeeFaker _employeeFaker;
    private readonly FakeOrganizationDbContext _dbContext;
    private readonly UpdateTeamMemberCommandHandler _handler;

    public UpdateTeamMemberCommandHandlerTests()
    {
        _teamFaker = new TeamFaker();
        _employeeFaker = new EmployeeFaker();
        _dbContext = new FakeOrganizationDbContext();

        _handler = new UpdateTeamMemberCommandHandler(
            _dbContext,
            _dbContext,
            new Mock<ILogger<UpdateTeamMemberCommandHandler>>().Object);
    }

    [Fact]
    public async Task Handle_ShouldAddNewRole_WhenRoleNotAlreadyAssigned()
    {
        // Arrange
        var team = _teamFaker.Generate();
        var employee = _employeeFaker.Generate();
        var roleId1 = Guid.NewGuid();
        var roleId2 = Guid.NewGuid();
        _dbContext.AddTeam(team);
        _dbContext.AddEmployee(employee);

        team.AddMember(employee, roleId1);

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
        var roleId1 = Guid.NewGuid();
        var roleId2 = Guid.NewGuid();
        _dbContext.AddTeam(team);
        _dbContext.AddEmployee(employee);

        team.AddMember(employee, roleId1);
        team.AddMember(employee, roleId2);

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
}
