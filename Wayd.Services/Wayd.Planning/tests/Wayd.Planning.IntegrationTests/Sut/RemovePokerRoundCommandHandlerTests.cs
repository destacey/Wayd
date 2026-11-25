using Moq;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.IntegrationTests.Infrastructure;
using Result = CSharpFunctionalExtensions.Result;

namespace Wayd.Planning.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="RemovePokerRoundCommandHandler"/> against a real SQL Server container, where a
/// participant's request arriving during the removal waits on the lock the removal holds on the round, and where the
/// order those locks are taken in decides whether the two deadlock — neither of which an in-memory fake has.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class RemovePokerRoundCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public RemovePokerRoundCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_WhenAVoteArrivesDuringTheRemoval_RemovesTheRoundAndTheVoteFindsItGone()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await PokerSeed.ActiveSessionWithVotingMember(_fixture, ct);
        var voteNotifier = new Mock<IPokerSessionNotifier>();
        var vote = new WaitingRequest<Result>(_fixture, (other, token) => PokerRequests.Vote(other, seed, "M", token, voteNotifier.Object));
        var notifier = new Mock<IPokerSessionNotifier>();

        // Act
        Result result;
        await using (var context = _fixture.CreateContext(vote))
        {
            result = await PokerRequests.Remove(context, seed, ct, notifier.Object);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var voted = await vote.Completion;
        voted.IsFailure.Should().BeTrue();
        voted.Error.Should().Be("Round not found.");
        await using var verify = _fixture.CreateContext();
        (await PokerSeed.Load(verify, seed.SessionId, ct)).Rounds.Should().BeEmpty();
        notifier.Verify(n => n.NotifyRoundRemoved(seed.SessionId, seed.RoundId), Times.Once);
        voteNotifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenTurningVotingOffArrivesDuringTheRemoval_BothSucceedWithoutDeadlocking()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await PokerSeed.ActiveSessionWithVote(_fixture, ct);
        var stopVoting = new WaitingRequest<CSharpFunctionalExtensions.Result<PokerSessionMemberDto>>(_fixture,
            (other, token) => PokerRequests.StopVoting(other, seed, token));

        // Act
        Result result;
        await using (var context = _fixture.CreateContext(stopVoting))
        {
            result = await PokerRequests.Remove(context, seed, ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var stopped = await stopVoting.Completion;
        stopped.IsSuccess.Should().BeTrue(stopped.IsFailure ? stopped.Error : null);
        await using var verify = _fixture.CreateContext();
        var saved = await PokerSeed.Load(verify, seed.SessionId, ct);
        saved.Rounds.Should().BeEmpty();
        saved.Members.Should().ContainSingle().Which.IsVoting.Should().BeFalse();
    }
}
