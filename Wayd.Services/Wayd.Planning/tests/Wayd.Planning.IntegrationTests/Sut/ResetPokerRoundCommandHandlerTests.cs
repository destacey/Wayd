using Moq;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.IntegrationTests.Infrastructure;
using Result = CSharpFunctionalExtensions.Result;

namespace Wayd.Planning.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="ResetPokerRoundCommandHandler"/> against a real SQL Server container, where a vote
/// arriving during the reset waits on the lock the reset holds on the round, which only a real database takes.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class ResetPokerRoundCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public ResetPokerRoundCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_WhenAVoteArrivesDuringTheReset_ClearsTheRoundAndTheVoteLandsInIt()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await PokerSeed.RevealedRoundWithVote(_fixture, ct);
        var vote = new WaitingRequest<Result>(_fixture, (other, token) => PokerRequests.Vote(other, seed, "M", token));
        var notifier = new Mock<IPokerSessionNotifier>();

        // Act
        Result result;
        await using (var context = _fixture.CreateContext(vote))
        {
            result = await PokerRequests.Reset(context, seed, ct, notifier.Object);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var voted = await vote.Completion;
        voted.IsSuccess.Should().BeTrue(voted.IsFailure ? voted.Error : null);
        await using var verify = _fixture.CreateContext();
        var round = (await PokerSeed.Load(verify, seed.SessionId, ct)).Rounds.Single();
        round.Status.Should().Be(PokerRoundStatus.Voting);
        round.Votes.Should().ContainSingle().Which.Value.Should().Be("M");
        notifier.Verify(n => n.NotifyRoundReset(seed.SessionId, seed.RoundId), Times.Once);
    }
}
