using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Organization.Application.TeamMemberRoles.Commands;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.TestData;

namespace Wayd.Organization.Application.Tests.Sut.TeamMemberRoles.Commands;

public class DeleteTeamMemberRoleCommandHandlerTests : IDisposable
{
    private readonly TeamFaker _teamFaker;
    private readonly EmployeeFaker _employeeFaker;
    private readonly FakeOrganizationDbContext _dbContext;
    private readonly DeleteTeamMemberRoleCommandHandler _handler;

    public DeleteTeamMemberRoleCommandHandlerTests()
    {
        _teamFaker = new TeamFaker();
        _employeeFaker = new EmployeeFaker();
        _dbContext = new FakeOrganizationDbContext();

        _handler = new DeleteTeamMemberRoleCommandHandler(
            _dbContext,
            new Mock<ILogger<DeleteTeamMemberRoleCommandHandler>>().Object);
    }

    [Fact]
    public async Task Handle_ShouldDeleteRole_WhenNotInUse()
    {
        // Arrange
        var role = TeamMemberRole.Create("Tech Lead", "Tech Lead role").Value;
        _dbContext.AddTeamMemberRole(role);

        var command = new DeleteTeamMemberRoleCommand(role.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenRoleIsInUse()
    {
        // Arrange
        var role = TeamMemberRole.Create("Tech Lead", "Tech Lead role").Value;
        var team = _teamFaker.Generate();
        var employee = _employeeFaker.Generate();
        _dbContext.AddTeamMemberRole(role);
        _dbContext.AddTeam(team);

        team.AddMember(employee, role.Id);
        _dbContext.AddTeamMember(team.Members.Single());

        var command = new DeleteTeamMemberRoleCommand(role.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("assigned to one or more team members");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenRoleNotFound()
    {
        // Arrange
        var command = new DeleteTeamMemberRoleCommand(Guid.NewGuid());

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
        GC.SuppressFinalize(this);
    }
}
