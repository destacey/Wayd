using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.TestData;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Commands;

public class RemoveTeamMemberCommandHandlerTests : IDisposable
{
    private readonly TeamFaker _teamFaker;
    private readonly EmployeeFaker _employeeFaker;
    private readonly FakeOrganizationDbContext _dbContext;
    private readonly RemoveTeamMemberCommandHandler _handler;

    public RemoveTeamMemberCommandHandlerTests()
    {
        _teamFaker = new TeamFaker();
        _employeeFaker = new EmployeeFaker();
        _dbContext = new FakeOrganizationDbContext();

        _handler = new RemoveTeamMemberCommandHandler(
            _dbContext,
            new Mock<ILogger<RemoveTeamMemberCommandHandler>>().Object);
    }

    [Fact]
    public async Task Handle_ShouldRemoveAllRoles_WhenEmployeeIsAMember()
    {
        // Arrange
        var team = _teamFaker.Generate();
        var employee = _employeeFaker.Generate();
        _dbContext.AddTeam(team);

        team.AddMember(employee, Guid.NewGuid());
        team.AddMember(employee, Guid.NewGuid());
        team.Members.Should().HaveCount(2);

        var command = new RemoveTeamMemberCommand(team.Id, employee.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.Members.Should().HaveCount(2);
        team.Members.Should().AllSatisfy(m => m.IsDeleted.Should().BeTrue());
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTeamNotFound()
    {
        // Arrange
        var command = new RemoveTeamMemberCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenEmployeeIsNotAMember()
    {
        // Arrange
        var team = _teamFaker.Generate();
        _dbContext.AddTeam(team);

        var command = new RemoveTeamMemberCommand(team.Id, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not a member");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
