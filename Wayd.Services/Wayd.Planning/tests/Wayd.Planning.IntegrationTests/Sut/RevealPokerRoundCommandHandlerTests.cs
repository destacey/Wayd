using Moq;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.IntegrationTests.Infrastructure;
using Result = CSharpFunctionalExtensions.Result;

namespace Wayd.Planning.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="RevealPokerRoundCommandHandler"/> against a real SQL Server container, where a
/// participant's request arriving during the reveal waits on the lock the reveal holds on the round, which only a real
/// database takes, and then finds the round revealed.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class RevealPokerRoundCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public RevealPokerRoundCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_WhenAVoteArrivesDuringTheReveal_RevealsWithoutItAndTheVoteIsRefused()
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
            result = await PokerRequests.Reveal(context, seed, ct, notifier.Object);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var voted = await vote.Completion;
        voted.IsFailure.Should().BeTrue();
        voted.Error.Should().Be("Votes can only be submitted when the round is in Voting status.");
        await using var verify = _fixture.CreateContext();
        var round = (await PokerSeed.Load(verify, seed.SessionId, ct)).Rounds.Single();
        round.Status.Should().Be(PokerRoundStatus.Revealed);
        round.Votes.Should().BeEmpty();
        notifier.Verify(n => n.NotifyVotesRevealed(seed.SessionId, seed.RoundId, It.Is<IEnumerable<PokerVoteDto>>(v => !v.Any())), Times.Once);
        voteNotifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenTurningVotingOffArrivesDuringTheReveal_RevealsTheVoteAndKeepsIt()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await PokerSeed.ActiveSessionWithVote(_fixture, ct);
        var votingNotifier = new Mock<IPokerSessionNotifier>();
        var stopVoting = new WaitingRequest<CSharpFunctionalExtensions.Result<PokerSessionMemberDto>>(_fixture,
            (other, token) => PokerRequests.StopVoting(other, seed, token, votingNotifier.Object));
        var notifier = new Mock<IPokerSessionNotifier>();

        // Act
        Result result;
        await using (var context = _fixture.CreateContext(stopVoting))
        {
            result = await PokerRequests.Reveal(context, seed, ct, notifier.Object);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var stopped = await stopVoting.Completion;
        stopped.IsSuccess.Should().BeTrue(stopped.IsFailure ? stopped.Error : null);
        stopped.Value.IsVoting.Should().BeFalse();
        await using var verify = _fixture.CreateContext();
        var saved = await PokerSeed.Load(verify, seed.SessionId, ct);
        saved.Members.Should().ContainSingle().Which.IsVoting.Should().BeFalse();
        var round = saved.Rounds.Single();
        round.Status.Should().Be(PokerRoundStatus.Revealed);
        round.Votes.Should().ContainSingle().Which.ParticipantId.Should().Be(seed.Voter.UserId);
        notifier.Verify(n => n.NotifyVotesRevealed(seed.SessionId, seed.RoundId, It.Is<IEnumerable<PokerVoteDto>>(v => v.Single().Value == "S")), Times.Once);
        votingNotifier.Verify(n => n.NotifyVoteWithdrawn(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAWithdrawalArrivesDuringTheReveal_RevealsTheVoteAndTheWithdrawalIsRefused()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await PokerSeed.ActiveSessionWithVote(_fixture, ct);
        var withdrawNotifier = new Mock<IPokerSessionNotifier>();
        var withdraw = new WaitingRequest<Result>(_fixture, (other, token) => PokerRequests.Withdraw(other, seed, token, withdrawNotifier.Object));
        var notifier = new Mock<IPokerSessionNotifier>();

        // Act
        Result result;
        await using (var context = _fixture.CreateContext(withdraw))
        {
            result = await PokerRequests.Reveal(context, seed, ct, notifier.Object);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var withdrawn = await withdraw.Completion;
        withdrawn.IsFailure.Should().BeTrue();
        withdrawn.Error.Should().Be("Votes can only be withdrawn when the round is in Voting status.");
        await using var verify = _fixture.CreateContext();
        var round = (await PokerSeed.Load(verify, seed.SessionId, ct)).Rounds.Single();
        round.Status.Should().Be(PokerRoundStatus.Revealed);
        round.Votes.Should().ContainSingle();
        notifier.Verify(n => n.NotifyVotesRevealed(seed.SessionId, seed.RoundId, It.Is<IEnumerable<PokerVoteDto>>(v => v.Single().Value == "S")), Times.Once);
        withdrawNotifier.VerifyNoOtherCalls();
    }
}
