using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Planning.Application.PokerSessions;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.IntegrationTests.Infrastructure;

namespace Wayd.Planning.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="SetPokerSessionVotingCommandHandler"/> against a real SQL Server container, where
/// a vote saved between the handler's read and its save is caught by the member row's version, which only a real
/// database enforces.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class SetPokerSessionVotingCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public SetPokerSessionVotingCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_TurningVotingOffWhenAVoteSavedFirst_WithdrawsThatVote()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await PokerSeed.ActiveSessionWithVotingMember(_fixture, ct);
        var access = new Mock<IPokerSessionAccess>();
        access.Setup(a => a.ResolveActor(It.IsAny<PokerSession>(), It.IsAny<CancellationToken>())).ReturnsAsync(seed.Voter);
        var notifier = new Mock<IPokerSessionNotifier>();
        var competitor = new CompetingSave(async token =>
        {
            await using var other = _fixture.CreateContext();
            var voted = await new SubmitVoteCommandHandler(other, access.Object, SqlServerDbContextFixture.DateTimeProvider(), Mock.Of<ICurrentUser>(), Mock.Of<IPokerSessionNotifier>())
                .Handle(new SubmitVoteCommand(seed.SessionId, seed.RoundId, "S"), token);
            voted.IsSuccess.Should().BeTrue(voted.IsFailure ? voted.Error : null);
        });

        // Act
        CSharpFunctionalExtensions.Result<PokerSessionMemberDto> result;
        await using (var context = _fixture.CreateContext(competitor))
        {
            result = await new SetPokerSessionVotingCommandHandler(context, access.Object, notifier.Object)
                .Handle(new SetPokerSessionVotingCommand(seed.SessionId, false), ct);
        }

        // Assert
        competitor.Made.Should().BeTrue();
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.IsVoting.Should().BeFalse();
        await using var verify = _fixture.CreateContext();
        var saved = await PokerSeed.Load(verify, seed.SessionId, ct);
        saved.Members.Should().ContainSingle().Which.IsVoting.Should().BeFalse();
        saved.Rounds.Single().Votes.Should().BeEmpty();
        notifier.Verify(n => n.NotifyVoteWithdrawn(seed.SessionId, seed.RoundId, seed.Voter.UserId), Times.Once);
    }

    [Fact]
    public async Task Handle_TurningVotingOffWhenARevealArrivesAsItSaves_WithdrawsTheVoteBeforeTheReveal()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await PokerSeed.ActiveSessionWithVote(_fixture, ct);
        var revealNotifier = new Mock<IPokerSessionNotifier>();
        var reveal = new WaitingRequest<CSharpFunctionalExtensions.Result>(_fixture, (other, token) => PokerRequests.Reveal(other, seed, token, revealNotifier.Object));
        var notifier = new Mock<IPokerSessionNotifier>();

        // Act
        CSharpFunctionalExtensions.Result<PokerSessionMemberDto> result;
        await using (var context = _fixture.CreateContext(reveal))
        {
            result = await PokerRequests.StopVoting(context, seed, ct, notifier.Object);
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
