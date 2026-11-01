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

public class WithdrawVoteCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly WithdrawVoteCommandHandler _handler;
    private readonly Mock<ILogger<WithdrawVoteCommandHandler>> _mockLogger;
    private readonly Mock<IPokerSessionNotifier> _mockNotifier;
    private readonly Mock<ICurrentUser> _mockCurrentUser;

    private readonly PokerSessionFaker _sessionFaker;
    private readonly string _currentUserId = Guid.NewGuid().ToString();

    public WithdrawVoteCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<WithdrawVoteCommandHandler>>();
        _mockNotifier = new Mock<IPokerSessionNotifier>();
        _mockCurrentUser = new Mock<ICurrentUser>();
        _mockCurrentUser.Setup(u => u.GetUserId()).Returns(_currentUserId);

        _handler = new WithdrawVoteCommandHandler(_dbContext, _mockCurrentUser.Object, _mockNotifier.Object, _mockLogger.Object);
        _sessionFaker = new PokerSessionFaker();
    }

    [Fact]
    public async Task Handle_ShouldWithdrawVote_WhenVoteExists()
    {
        // Arrange
        var scale = new EstimationScaleFaker().Generate();
        _dbContext.AddEstimationScale(scale);
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).WithEstimationScale(scale).Generate();
        _dbContext.AddPokerSession(session);

        session.AddRound("Story");
        var round = session.Rounds.First();
        session.SubmitVote(round.Id, _currentUserId, "8", scale.CurrentVersion, Instant.FromUtc(2026, 1, 15, 9, 0));

        var command = new WithdrawVoteCommand(session.Id, round.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        round.Votes.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyVoteWithdrawn(session.Id, round.Id, _currentUserId), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenNoVoteExists()
    {
        // Arrange
        var scale = new EstimationScaleFaker().Generate();
        _dbContext.AddEstimationScale(scale);
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).WithEstimationScale(scale).Generate();
        _dbContext.AddPokerSession(session);

        session.AddRound("Story");
        var round = session.Rounds.First();

        var command = new WithdrawVoteCommand(session.Id, round.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No vote found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenSessionDoesNotExist()
    {
        // Arrange
        var command = new WithdrawVoteCommand(Guid.NewGuid(), Guid.NewGuid());

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
