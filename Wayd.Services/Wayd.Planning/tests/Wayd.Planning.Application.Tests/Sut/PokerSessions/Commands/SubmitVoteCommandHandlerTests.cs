using Wayd.Common.Application.Interfaces;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Tests.Data;
using Moq;
using Wayd.Common.Domain.Events;
using Wayd.Planning.Domain.Models.PlanningPoker;
using NodaTime;
using Microsoft.EntityFrameworkCore;
using Wayd.Tests.Shared.Infrastructure;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Commands;

public class SubmitVoteCommandHandlerTests : IDisposable
{
    private readonly FakePokerSessionAccess _access = new();
    private readonly FakePlanningDbContext _dbContext;
    private readonly SubmitVoteCommandHandler _handler;
    private readonly Mock<IPokerSessionNotifier> _mockNotifier;
    private readonly Mock<ICurrentUser> _mockCurrentUser;
    private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;

    private readonly PokerSessionFaker _sessionFaker;
    private readonly string _currentUserId = Guid.NewGuid().ToString();

    public SubmitVoteCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockNotifier = new Mock<IPokerSessionNotifier>();
        _mockCurrentUser = new Mock<ICurrentUser>();
        _mockCurrentUser.Setup(u => u.GetUserId()).Returns(_currentUserId);
        _access.Actor = PokerSessionActors.Voter(_currentUserId);
        _dbContext.AddEmployee(new EmployeeFaker().WithId(_access.Actor.EmployeeId).Generate());
        _mockCurrentUser.Setup(u => u.Name).Returns("Test User");
        _mockDateTimeProvider = new Mock<IDateTimeProvider>();
        _mockDateTimeProvider.Setup(d => d.Now).Returns(Instant.FromUtc(2026, 1, 15, 10, 0));

        _handler = new SubmitVoteCommandHandler(_dbContext, _access, _mockDateTimeProvider.Object, _mockCurrentUser.Object, _mockNotifier.Object);
        _sessionFaker = new PokerSessionFaker();
    }

    [Fact]
    public async Task Handle_ShouldSubmitVote_WhenRoundIsVoting()
    {
        // Arrange
        var scale = new EstimationScaleFaker().Generate();
        _dbContext.AddEstimationScale(scale);
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).WithEstimationScale(scale).Generate();
        _dbContext.AddPokerSession(session);

        session.AddRound("Story", null, PokerSessionActors.Facilitator);
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
        var scale = new EstimationScaleFaker().Generate();
        _dbContext.AddEstimationScale(scale);
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).WithEstimationScale(scale).Generate();
        _dbContext.AddPokerSession(session);

        session.AddRound("Story", null, PokerSessionActors.Facilitator);
        var round = session.Rounds.First();


        // First vote
        session.SubmitVote(round.Id, "5", scale.CurrentVersion, PokerSessionActors.Voter(_currentUserId), Instant.FromUtc(2026, 1, 15, 9, 0));

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
    public async Task Handle_WhenAnotherRequestSavedTheFirstVote_ShouldRetryAndSucceed()
    {
        // Arrange
        var scale = new EstimationScaleFaker().Generate();
        _dbContext.AddEstimationScale(scale);
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).WithEstimationScale(scale).Generate();
        _dbContext.AddPokerSession(session);
        session.AddRound("Story", null, PokerSessionActors.Facilitator);
        var round = session.Rounds.First();
        _dbContext.FailNextSaveWith = SaveFailures.UniqueKey();

        // Act
        var result = await _handler.Handle(new SubmitVoteCommand(session.Id, round.Id, "8"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        round.Votes.Should().ContainSingle().Which.Value.Should().Be("8");
        _dbContext.SaveChangesCallCount.Should().Be(2);
        _mockNotifier.Verify(n => n.NotifyVoteSubmitted(session.Id, round.Id, _currentUserId, "Test User"), Times.Once);
    }

    [Fact]
    public async Task Handle_OnAConcurrencyConflict_ShouldRetryOnce()
    {
        // Arrange
        var scale = new EstimationScaleFaker().Generate();
        _dbContext.AddEstimationScale(scale);
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).WithEstimationScale(scale).Generate();
        _dbContext.AddPokerSession(session);
        session.AddRound("Story", null, PokerSessionActors.Facilitator);
        var round = session.Rounds.First();
        _dbContext.ConcurrencyConflictsToThrow = 1;

        // Act
        var result = await _handler.Handle(new SubmitVoteCommand(session.Id, round.Id, "8"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        round.Votes.Should().ContainSingle().Which.Value.Should().Be("8");
        _dbContext.SaveChangesCallCount.Should().Be(2);
        _mockNotifier.Verify(n => n.NotifyVoteSubmitted(session.Id, round.Id, _currentUserId, "Test User"), Times.Once);
    }

    [Fact]
    public async Task Handle_OnASecondConcurrencyConflict_ShouldThrow()
    {
        // Arrange
        var scale = new EstimationScaleFaker().Generate();
        _dbContext.AddEstimationScale(scale);
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).WithEstimationScale(scale).Generate();
        _dbContext.AddPokerSession(session);
        session.AddRound("Story", null, PokerSessionActors.Facilitator);
        var round = session.Rounds.First();
        _dbContext.ConcurrencyConflictsToThrow = 2;

        // Act
        var act = () => _handler.Handle(new SubmitVoteCommand(session.Id, round.Id, "8"), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        _dbContext.SaveChangesCallCount.Should().Be(2);
        _mockNotifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenTheSaveFailsForAnotherReason_ShouldNotRetry()
    {
        // Arrange
        var scale = new EstimationScaleFaker().Generate();
        _dbContext.AddEstimationScale(scale);
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).WithEstimationScale(scale).Generate();
        _dbContext.AddPokerSession(session);
        session.AddRound("Story", null, PokerSessionActors.Facilitator);
        var round = session.Rounds.First();
        _dbContext.FailNextSaveWith = SaveFailures.Truncation();

        // Act
        var act = () => _handler.Handle(new SubmitVoteCommand(session.Id, round.Id, "8"), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.VerifyNoOtherCalls();
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
        var scale = new EstimationScaleFaker().Generate();
        _dbContext.AddEstimationScale(scale);
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).WithEstimationScale(scale).Generate();
        _dbContext.AddPokerSession(session);

        session.AddRound("Story", null, PokerSessionActors.Facilitator);
        var round = session.Rounds.First();
        session.SubmitVote(round.Id, "3", scale.CurrentVersion, PokerSessionActors.Voter(Guid.NewGuid().ToString()), Instant.FromUtc(2026, 1, 15, 9, 0));
        session.RevealRound(round.Id, PokerSessionActors.Facilitator);
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
        var scale = new EstimationScaleFaker().Generate();
        _dbContext.AddEstimationScale(scale);
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).WithEstimationScale(scale).Generate();
        _dbContext.AddPokerSession(session);

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
        var scale = new EstimationScaleFaker().WithValues("S", "M", "L", "?").Generate();
        _dbContext.AddEstimationScale(scale);
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).WithEstimationScale(scale).Generate();
        _dbContext.AddPokerSession(session);

        session.AddRound("Story", null, PokerSessionActors.Facilitator);
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

    [Fact]
    public async Task Handle_AfterTheScalePublishesANewVersion_ShouldCheckAgainstThePinnedVersion()
    {
        // Arrange
        var scale = new EstimationScaleFaker().WithValues("S", "M", "L", "?").Generate();
        _dbContext.AddEstimationScale(scale);
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).WithEstimationScale(scale).Generate();
        _dbContext.AddPokerSession(session);
        session.AddRound("Story", null, PokerSessionActors.Facilitator);
        var round = session.Rounds.First();
        scale.PublishVersion([new EstimationScaleValue("Small", true), new EstimationScaleValue("Medium", true)], EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 0));

        // Act
        var newValue = await _handler.Handle(new SubmitVoteCommand(session.Id, round.Id, "Medium"), TestContext.Current.CancellationToken);
        var pinnedValue = await _handler.Handle(new SubmitVoteCommand(session.Id, round.Id, "M"), TestContext.Current.CancellationToken);

        // Assert
        newValue.IsFailure.Should().BeTrue();
        newValue.Error.Should().Be("'Medium' is not a value on the session's estimation scale.");
        pinnedValue.IsSuccess.Should().BeTrue(pinnedValue.IsFailure ? pinnedValue.Error : null);
        round.Votes.Should().ContainSingle().Which.Value.Should().Be("M");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
