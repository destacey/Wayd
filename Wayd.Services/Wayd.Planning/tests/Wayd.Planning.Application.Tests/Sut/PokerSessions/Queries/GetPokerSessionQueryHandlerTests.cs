using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Identity;
using Wayd.Planning.Application.PokerSessions.Queries;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.Domain.Tests.Data;
using Wayd.Tests.Shared.Data;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Queries;

/// <summary>
/// Covers the session detail query, chiefly that a round's vote values stay hidden from other
/// participants until the round is revealed.
/// </summary>
public class GetPokerSessionQueryHandlerTests : IDisposable
{
    private static readonly Instant VotedOn = Instant.FromUtc(2026, 1, 15, 10, 0);

    private readonly FakePlanningDbContext _dbContext;
    private readonly Mock<ICurrentUser> _mockCurrentUser;
    private readonly GetPokerSessionQueryHandler _handler;

    private readonly User _viewer;
    private readonly User _otherParticipant;
    private readonly EstimationScale _scale = new EstimationScaleFaker().WithValues("1", "2", "3", "5", "8", "?").Generate();

    public GetPokerSessionQueryHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _viewer = new UserFaker().Generate();
        _otherParticipant = new UserFaker().Generate();

        _mockCurrentUser = new Mock<ICurrentUser>();
        _mockCurrentUser.Setup(u => u.GetUserId()).Returns(_viewer.Id);

        _handler = new GetPokerSessionQueryHandler(_dbContext, _mockCurrentUser.Object);
    }

    private PokerSession CreateSessionWithVotes()
    {
        var session = new PokerSessionFaker().WithStatus(PokerSessionStatus.Active).WithEstimationScale(_scale).Generate();
        session.AddRound("Story to estimate");
        var round = session.Rounds.First();
        session.SubmitVote(round.Id, _viewer.Id, "5", _scale.CurrentVersion, VotedOn);
        session.SubmitVote(round.Id, _otherParticipant.Id, "8", _scale.CurrentVersion, VotedOn);

        LinkParticipants(round);
        _dbContext.AddPokerSession(session);
        return session;
    }

    /// <summary>
    /// Sets each vote's <see cref="PokerVote.Participant"/> navigation, which EF loads in production
    /// but the in-memory fake leaves null.
    /// </summary>
    private void LinkParticipants(PokerRound round)
    {
        var users = new[] { _viewer, _otherParticipant }.ToDictionary(u => u.Id);
        var participant = typeof(PokerVote).GetProperty(nameof(PokerVote.Participant))!;
        foreach (var vote in round.Votes)
            participant.SetValue(vote, users[vote.ParticipantId]);
    }

    [Fact]
    public async Task Handle_ShouldHideOtherParticipantsVoteValues_WhenRoundIsVoting()
    {
        // Arrange
        var session = CreateSessionWithVotes();

        // Act
        var result = await _handler.Handle(
            new GetPokerSessionQuery(new IdOrKey(session.Id.ToString())),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        var votes = result!.Rounds.Single().Votes;
        votes.Should().HaveCount(2);
        votes.Single(v => v.Participant!.Id == Guid.Parse(_otherParticipant.Id)).Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldKeepTheCallersOwnVoteValue_WhenRoundIsVoting()
    {
        // Arrange
        var session = CreateSessionWithVotes();

        // Act
        var result = await _handler.Handle(
            new GetPokerSessionQuery(new IdOrKey(session.Id.ToString())),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        var votes = result!.Rounds.Single().Votes;
        votes.Single(v => v.Participant!.Id == Guid.Parse(_viewer.Id)).Value.Should().Be("5");
    }

    [Fact]
    public async Task Handle_ShouldHideEveryVoteValue_WhenCallerHasNoUserId()
    {
        // Arrange
        var session = CreateSessionWithVotes();
        _mockCurrentUser.Setup(u => u.GetUserId()).Returns(string.Empty);

        // Act
        var result = await _handler.Handle(
            new GetPokerSessionQuery(new IdOrKey(session.Id.ToString())),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Rounds.Single().Votes.Should().OnlyContain(v => v.Value == string.Empty);
    }

    [Fact]
    public async Task Handle_ShouldReturnEveryVoteValue_WhenRoundIsRevealed()
    {
        // Arrange
        var session = CreateSessionWithVotes();
        session.RevealRound(session.Rounds.First().Id);

        // Act
        var result = await _handler.Handle(
            new GetPokerSessionQuery(new IdOrKey(session.Id.ToString())),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Rounds.Single().Votes.Select(v => v.Value).Should().BeEquivalentTo(["5", "8"]);
    }

    [Fact]
    public async Task Handle_ShouldReturnEveryVoteValue_WhenRoundIsAccepted()
    {
        // Arrange
        var session = CreateSessionWithVotes();
        var roundId = session.Rounds.First().Id;
        session.RevealRound(roundId);
        session.SetConsensus(roundId, "8", _scale.CurrentVersion);

        // Act
        var result = await _handler.Handle(
            new GetPokerSessionQuery(new IdOrKey(session.Id.ToString())),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        var round = result!.Rounds.Single();
        round.Status.Should().Be(nameof(PokerRoundStatus.Accepted));
        round.Votes.Select(v => v.Value).Should().BeEquivalentTo(["5", "8"]);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenSessionDoesNotExist()
    {
        // Arrange
        CreateSessionWithVotes();

        // Act
        var result = await _handler.Handle(
            new GetPokerSessionQuery(new IdOrKey(Guid.NewGuid().ToString())),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
