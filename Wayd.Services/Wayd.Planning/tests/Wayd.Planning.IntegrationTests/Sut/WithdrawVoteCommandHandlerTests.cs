using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Planning.Application.PokerSessions;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.IntegrationTests.Infrastructure;

namespace Wayd.Planning.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="WithdrawVoteCommandHandler"/> against a real SQL Server container, where another
/// request deleting the same vote first makes this one's delete affect no row, which only a real database reports.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class WithdrawVoteCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public WithdrawVoteCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_WhenTurningVotingOffWithdrewTheVoteFirst_FailsAsNoVote()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await PokerSeed.ActiveSessionWithVotingMember(_fixture, ct);
        await using (var voting = _fixture.CreateContext())
        {
            var session = await PokerSeed.Load(voting, seed.SessionId, ct);
            var version = await voting.EstimationScaleVersions.AsNoTracking()
                .SingleAsync(v => v.Id == session.EstimationScaleVersionId, ct);
            session.SubmitVote(seed.RoundId, "S", version, seed.Voter, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            await voting.SaveChangesAsync(ct);
        }
        var access = new Mock<IPokerSessionAccess>();
        access.Setup(a => a.ResolveActor(It.IsAny<PokerSession>(), It.IsAny<CancellationToken>())).ReturnsAsync(seed.Voter);
        var notifier = new Mock<IPokerSessionNotifier>();
        var competitor = new CompetingSave(async token =>
        {
            await using var other = _fixture.CreateContext();
            var set = await new SetPokerSessionVotingCommandHandler(other, access.Object, Mock.Of<IPokerSessionNotifier>())
                .Handle(new SetPokerSessionVotingCommand(seed.SessionId, false), token);
            set.IsSuccess.Should().BeTrue(set.IsFailure ? set.Error : null);
        });

        // Act
        CSharpFunctionalExtensions.Result result;
        await using (var context = _fixture.CreateContext(competitor))
        {
            result = await new WithdrawVoteCommandHandler(context, access.Object, notifier.Object)
                .Handle(new WithdrawVoteCommand(seed.SessionId, seed.RoundId), ct);
        }

        // Assert
        competitor.Made.Should().BeTrue();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("No vote found for this participant.");
        await using var verify = _fixture.CreateContext();
        var saved = await PokerSeed.Load(verify, seed.SessionId, ct);
        saved.Rounds.Single().Votes.Should().BeEmpty();
        notifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenARevealArrivesAsItSaves_WithdrawsTheVoteBeforeTheReveal()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await PokerSeed.ActiveSessionWithVote(_fixture, ct);
        var revealNotifier = new Mock<IPokerSessionNotifier>();
        var reveal = new WaitingRequest<CSharpFunctionalExtensions.Result>(_fixture, (other, token) => PokerRequests.Reveal(other, seed, token, revealNotifier.Object));
        var notifier = new Mock<IPokerSessionNotifier>();

        // Act
        CSharpFunctionalExtensions.Result result;
        await using (var context = _fixture.CreateContext(reveal))
        {
            result = await PokerRequests.Withdraw(context, seed, ct, notifier.Object);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var revealed = await reveal.Completion;
        revealed.IsSuccess.Should().BeTrue(revealed.IsFailure ? revealed.Error : null);
        await using var verify = _fixture.CreateContext();
        var round = (await PokerSeed.Load(verify, seed.SessionId, ct)).Rounds.Single();
        round.Status.Should().Be(PokerRoundStatus.Revealed);
        round.Votes.Should().BeEmpty();
        notifier.Verify(n => n.NotifyVoteWithdrawn(seed.SessionId, seed.RoundId, seed.Voter.UserId), Times.Once);
        revealNotifier.Verify(n => n.NotifyVotesRevealed(seed.SessionId, seed.RoundId, It.Is<IEnumerable<PokerVoteDto>>(v => !v.Any())), Times.Once);
    }
}
