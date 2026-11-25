using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.IntegrationTests.Infrastructure;
using Result = CSharpFunctionalExtensions.Result;

namespace Wayd.Planning.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="DeletePokerSessionCommandHandler"/> against a real SQL Server container, where a
/// vote arriving during the deletion waits on the lock the deletion holds on every round, which only a real database
/// takes.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class DeletePokerSessionCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public DeletePokerSessionCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_WhenAVoteArrivesDuringTheDeletion_DeletesTheSessionAndTheVoteFindsItGone()
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
            result = await PokerRequests.Delete(context, seed, ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var voted = await vote.Completion;
        voted.IsFailure.Should().BeTrue();
        voted.Error.Should().Be("Poker session not found.");
        await using var verify = _fixture.CreateContext();
        (await verify.PokerSessions.AnyAsync(s => s.Id == seed.SessionId, ct)).Should().BeFalse();
        voteNotifier.VerifyNoOtherCalls();
    }
}
