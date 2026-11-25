using Moq;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.IntegrationTests.Infrastructure;
using Result = CSharpFunctionalExtensions.Result;

namespace Wayd.Planning.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="CompletePokerSessionCommandHandler"/> against a real SQL Server container, where a
/// vote arriving during completion waits on the lock completion holds on every round, which only a real database
/// takes.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class CompletePokerSessionCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public CompletePokerSessionCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_WhenAVoteArrivesDuringCompletion_CompletesTheSessionAndTheVoteIsRefused()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await PokerSeed.ActiveSessionWithVotingMember(_fixture, ct);
        var voteNotifier = new Mock<IPokerSessionNotifier>();
        var vote = new WaitingRequest<Result>(_fixture, (other, token) => PokerRequests.Vote(other, seed, "M", token, voteNotifier.Object));

        // Act
        Result result;
        await using (var context = _fixture.CreateContext(vote))
        {
            result = await PokerRequests.Complete(context, seed, ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var voted = await vote.Completion;
        voted.IsFailure.Should().BeTrue();
        voted.Error.Should().Be("Cannot vote when the session is not active.");
        await using var verify = _fixture.CreateContext();
        var saved = await PokerSeed.Load(verify, seed.SessionId, ct);
        saved.Status.Should().Be(PokerSessionStatus.Completed);
        saved.Rounds.Single().Votes.Should().BeEmpty();
        voteNotifier.VerifyNoOtherCalls();
    }
}
