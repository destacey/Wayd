using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.TestData;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Commands;

public class AddTeamMemberCommandHandlerTests : IDisposable
{
    private readonly TeamFaker _teamFaker;
    private readonly EmployeeFaker _employeeFaker;
    private readonly FakeOrganizationDbContext _dbContext;
    private readonly AddTeamMemberCommandHandler _handler;

    public AddTeamMemberCommandHandlerTests()
    {
        _teamFaker = new TeamFaker();
        _employeeFaker = new EmployeeFaker();
        _dbContext = new FakeOrganizationDbContext();

        _handler = new AddTeamMemberCommandHandler(
            _dbContext,
            _dbContext,
            new Mock<ILogger<AddTeamMemberCommandHandler>>().Object);
    }

    [Fact]
    public async Task Handle_ShouldAddMember_WithSingleRole()
    {
        // Arrange
        var team = _teamFaker.Generate();
        var employee = _employeeFaker.Generate();
        var roleId = Guid.NewGuid();
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
        var roleId1 = Guid.NewGuid();
        var roleId2 = Guid.NewGuid();
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
        var roleId = Guid.NewGuid();
        _dbContext.AddTeam(team);
        _dbContext.AddEmployee(employee);

        team.AddMember(employee, roleId);

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

        var command = new AddTeamMemberCommand(team.Id, employee.Id, [Guid.NewGuid()]);

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

        var command = new AddTeamMemberCommand(team.Id, employee.Id, [Guid.NewGuid()]);

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
}
