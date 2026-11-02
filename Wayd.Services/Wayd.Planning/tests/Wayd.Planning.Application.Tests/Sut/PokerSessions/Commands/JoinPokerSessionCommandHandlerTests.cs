using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Commands;

public class JoinPokerSessionCommandHandlerTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 10, 0);

    private readonly FakePokerSessionAccess _access = new();
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<IPokerSessionNotifier> _mockNotifier = new();
    private readonly JoinPokerSessionCommandHandler _handler;
    private readonly EstimationScale _scale = new EstimationScaleFaker().Generate();
    private readonly PokerSessionActor _voter = PokerSessionActors.Voter("user-1");

    public JoinPokerSessionCommandHandlerTests()
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.Now).Returns(Now);

        _access.Actor = _voter;
        _dbContext.AddEmployee(new EmployeeFaker().WithId(_voter.EmployeeId).Generate());
        _dbContext.AddEstimationScale(_scale);

        _handler = new JoinPokerSessionCommandHandler(_dbContext, _access, dateTimeProvider.Object, _mockNotifier.Object, new Mock<ILogger<JoinPokerSessionCommandHandler>>().Object);
    }

    [Fact]
    public async Task Handle_ShouldJoinAndAnnounceTheMember()
    {
        // Arrange
        var session = new PokerSessionFaker().WithEstimationScale(_scale).Generate();
        _dbContext.AddPokerSession(session);

        // Act
        var result = await _handler.Handle(new JoinPokerSessionCommand(session.Id, PokerSessionMemberMode.Observer), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Employee.Id.Should().Be(_voter.EmployeeId);
        result.Value.Mode.Should().Be(PokerSessionMemberMode.Observer);
        result.Value.JoinedOn.Should().Be(Now);
        session.PresentMember(_voter.EmployeeId).Should().NotBeNull();
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyMemberUpdated(session.Id, It.Is<PokerSessionMemberDto>(m => m.Employee.Id == _voter.EmployeeId)), Times.Once);
    }

    [Fact]
    public async Task Handle_AgainAsObserver_ShouldAnnounceTheWithdrawnVote()
    {
        // Arrange
        var session = new PokerSessionFaker().WithEstimationScale(_scale).Generate();
        var round = session.AddRound("Story", null, PokerSessionActors.Facilitator).Value;
        session.SubmitVote(round.Id, _scale.CurrentVersion.Values.First().Value, _scale.CurrentVersion, _voter, Now);
        _dbContext.AddPokerSession(session);

        // Act
        var result = await _handler.Handle(new JoinPokerSessionCommand(session.Id, PokerSessionMemberMode.Observer), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        round.Votes.Should().BeEmpty();
        _mockNotifier.Verify(n => n.NotifyVoteWithdrawn(session.Id, round.Id, _voter.UserId), Times.Once);
    }

    [Fact]
    public async Task Handle_PrivateSessionNotInvited_ShouldReadAsNotFound()
    {
        // Arrange
        var session = new PokerSessionFaker().WithEstimationScale(_scale).WithVisibility(Visibility.Private).Generate();
        _dbContext.AddPokerSession(session);

        // Act
        var result = await _handler.Handle(new JoinPokerSessionCommand(session.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PokerSession.NotFoundError);
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.VerifyNoOtherCalls();
    }

    public void Dispose() => _dbContext.Dispose();
}
