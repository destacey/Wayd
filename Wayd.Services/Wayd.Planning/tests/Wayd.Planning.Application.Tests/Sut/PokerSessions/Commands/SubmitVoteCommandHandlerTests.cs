using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Interfaces;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Tests.Data;
using Moq;
using NodaTime;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Commands;

public class SubmitVoteCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly SubmitVoteCommandHandler _handler;
    private readonly Mock<ILogger<SubmitVoteCommandHandler>> _mockLogger;
    private readonly Mock<IPokerSessionNotifier> _mockNotifier;
    private readonly Mock<ICurrentUser> _mockCurrentUser;
    private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;

    private readonly PokerSessionFaker _sessionFaker;
    private readonly string _currentUserId = Guid.NewGuid().ToString();

    public SubmitVoteCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<SubmitVoteCommandHandler>>();
        _mockNotifier = new Mock<IPokerSessionNotifier>();
        _mockCurrentUser = new Mock<ICurrentUser>();
        _mockCurrentUser.Setup(u => u.GetUserId()).Returns(_currentUserId);
        _mockCurrentUser.Setup(u => u.Name).Returns("Test User");
        _mockDateTimeProvider = new Mock<IDateTimeProvider>();
        _mockDateTimeProvider.Setup(d => d.Now).Returns(Instant.FromUtc(2026, 1, 15, 10, 0));

        _handler = new SubmitVoteCommandHandler(_dbContext, _mockDateTimeProvider.Object, _mockCurrentUser.Object, _mockNotifier.Object, _mockLogger.Object);
        _sessionFaker = new PokerSessionFaker();
    }

    [Fact]
    public async Task Handle_ShouldSubmitVote_WhenRoundIsVoting()
    {
        // Arrange
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).Generate();
        _dbContext.AddPokerSession(session);
        _dbContext.AddEstimationScale(new EstimationScaleFaker().WithId(session.EstimationScaleId).Generate());

        session.AddRound("Story");
        var round = session.Rounds.First();


        var command = new SubmitVoteCommand(session.Id, round.Id, "8");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        round.Votes.Should().HaveCount(1);
        round.Votes.First().Value.Should().Be("8");
        round.Votes.First().ParticipantId.Should().Be(_currentUserId);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyVoteSubmitted(session.Id, round.Id, _currentUserId, "Test User"), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldUpdateVote_WhenParticipantVotesAgain()
    {
        // Arrange
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).Generate();
        _dbContext.AddPokerSession(session);
        _dbContext.AddEstimationScale(new EstimationScaleFaker().WithId(session.EstimationScaleId).Generate());

        session.AddRound("Story");
        var round = session.Rounds.First();


        // First vote
        session.SubmitVote(round.Id, _currentUserId, "5", new EstimationScaleFaker().WithId(session.EstimationScaleId).Generate(), Instant.FromUtc(2026, 1, 15, 9, 0));

        var command = new SubmitVoteCommand(session.Id, round.Id, "8");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        round.Votes.Should().HaveCount(1);
        round.Votes.First().Value.Should().Be("8");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenSessionDoesNotExist()
    {
        // Arrange
        var command = new SubmitVoteCommand(Guid.NewGuid(), Guid.NewGuid(), "5");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenRoundIsNotVoting()
    {
        // Arrange
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).Generate();
        _dbContext.AddPokerSession(session);
        _dbContext.AddEstimationScale(new EstimationScaleFaker().WithId(session.EstimationScaleId).Generate());

        session.AddRound("Story");
        var round = session.Rounds.First();
        session.SubmitVote(round.Id, Guid.NewGuid().ToString(), "3", new EstimationScaleFaker().WithId(session.EstimationScaleId).Generate(), Instant.FromUtc(2026, 1, 15, 9, 0));
        session.RevealRound(round.Id);
        // Round is now Revealed, not Voting

        var command = new SubmitVoteCommand(session.Id, round.Id, "5");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyVoteSubmitted(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenRoundDoesNotExist()
    {
        // Arrange
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).Generate();
        _dbContext.AddPokerSession(session);
        _dbContext.AddEstimationScale(new EstimationScaleFaker().WithId(session.EstimationScaleId).Generate());

        var command = new SubmitVoteCommand(session.Id, Guid.NewGuid(), "5");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenValueIsNotOnTheSessionsScale()
    {
        // Arrange
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).Generate();
        _dbContext.AddPokerSession(session);
        _dbContext.AddEstimationScale(new EstimationScaleFaker().WithId(session.EstimationScaleId).WithValues("S", "M", "L", "?").Generate());

        session.AddRound("Story");
        var round = session.Rounds.First();

        var command = new SubmitVoteCommand(session.Id, round.Id, "8");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("'8' is not a value on the session's estimation scale.");
        round.Votes.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyVoteSubmitted(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
