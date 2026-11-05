using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Commands;

public class SetPokerSessionInviteesCommandHandlerTests : IDisposable
{
    private readonly FakePokerSessionAccess _access = new();
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<IPokerSessionNotifier> _mockNotifier = new();
    private readonly SetPokerSessionInviteesCommandHandler _handler;

    public SetPokerSessionInviteesCommandHandlerTests()
    {
        _handler = new SetPokerSessionInviteesCommandHandler(_dbContext, _access, _mockNotifier.Object, new Mock<ILogger<SetPokerSessionInviteesCommandHandler>>().Object);
    }

    [Fact]
    public async Task Handle_ShouldReplaceTheInvitees()
    {
        // Arrange
        var session = new PokerSessionFaker().WithInvitedEmployees(Guid.NewGuid()).Generate();
        _dbContext.AddPokerSession(session);
        var employee = new EmployeeFaker().Generate();
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        _dbContext.AddEmployee(employee);
        _dbContext.AddPlanningTeam(team);

        // Act
        var result = await _handler.Handle(new SetPokerSessionInviteesCommand(session.Id, [employee.Id], [team.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        session.InvitedEmployees.Select(e => e.EmployeeId).Should().Equal(employee.Id);
        session.InvitedTeams.Select(t => t.TeamId).Should().Equal(team.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyAccessChanged(session.Id), Times.Once);
    }

    [Fact]
    public async Task Handle_UnknownTeam_ShouldFail()
    {
        // Arrange
        var session = new PokerSessionFaker().Generate();
        _dbContext.AddPokerSession(session);

        // Act
        var result = await _handler.Handle(new SetPokerSessionInviteesCommand(session.Id, [], [Guid.NewGuid()]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        session.InvitedTeams.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
