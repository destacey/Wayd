using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Tests.Data;
using Moq;
using NodaTime;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Commands;

public class SetConsensusCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly SetConsensusCommandHandler _handler;
    private readonly Mock<ILogger<SetConsensusCommandHandler>> _mockLogger;
    private readonly Mock<IPokerSessionNotifier> _mockNotifier;

    private readonly PokerSessionFaker _sessionFaker;

    public SetConsensusCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<SetConsensusCommandHandler>>();
        _mockNotifier = new Mock<IPokerSessionNotifier>();

        _handler = new SetConsensusCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
        _sessionFaker = new PokerSessionFaker();
    }

    [Fact]
    public async Task Handle_ShouldSetConsensus_WhenRoundIsRevealed()
    {
        // Arrange
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).Generate();
        _dbContext.AddPokerSession(session);

        session.AddRound("Story");
        var round = session.Rounds.First();
        session.SubmitVote(round.Id, Guid.NewGuid().ToString(), "5", new EstimationScaleFaker().WithId(session.EstimationScaleId).Generate(), Instant.FromUtc(2026, 1, 15, 10, 0));
        session.RevealRound(round.Id);

        var command = new SetConsensusCommand(session.Id, round.Id, "5");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        round.Status.Should().Be(PokerRoundStatus.Accepted);
        round.ConsensusEstimate.Should().Be("5");
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyConsensusSet(session.Id, round.Id, "5"), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenSessionDoesNotExist()
    {
        // Arrange
        var command = new SetConsensusCommand(Guid.NewGuid(), Guid.NewGuid(), "5");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenRoundIsNotRevealed()
    {
        // Arrange
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).Generate();
        _dbContext.AddPokerSession(session);

        session.AddRound("Story");
        var round = session.Rounds.First();
        // Round is Voting, not Revealed

        var command = new SetConsensusCommand(session.Id, round.Id, "5");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyConsensusSet(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenRoundDoesNotExist()
    {
        // Arrange
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).Generate();
        _dbContext.AddPokerSession(session);

        var command = new SetConsensusCommand(session.Id, Guid.NewGuid(), "5");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
