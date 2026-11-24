using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Planning.Application.PokerSessions;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.IntegrationTests.Infrastructure;

namespace Wayd.Planning.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="SubmitVoteCommandHandler"/> against a real SQL Server container, where a
/// double-submitted first vote meets the earlier one at a unique index and is retried against a reload that only
/// a real change tracker can get wrong.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class SubmitVoteCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public SubmitVoteCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_WhenAnotherFirstVoteSavedFirst_ReplacesItWithThisVote()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await PokerSeed.ActiveSession(_fixture, ct);
        var competitor = new CompetingSave(async token =>
        {
            await using var other = _fixture.CreateContext();
            var session = await PokerSeed.Load(other, seed.SessionId, token);
            var version = await other.EstimationScaleVersions.AsNoTracking()
                .SingleAsync(v => v.Id == session.EstimationScaleVersionId, token);
            session.SubmitVote(seed.RoundId, "S", version, seed.Voter, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            await other.SaveChangesAsync(token);
        });
        var access = new Mock<IPokerSessionAccess>();
        access.Setup(a => a.ResolveActor(It.IsAny<PokerSession>(), It.IsAny<CancellationToken>())).ReturnsAsync(seed.Voter);
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.Name).Returns("Ada Lovelace");
        var notifier = new Mock<IPokerSessionNotifier>();

        // Act
        CSharpFunctionalExtensions.Result result;
        await using (var context = _fixture.CreateContext(competitor))
        {
            result = await new SubmitVoteCommandHandler(context, access.Object, SqlServerDbContextFixture.DateTimeProvider(), currentUser.Object, notifier.Object)
                .Handle(new SubmitVoteCommand(seed.SessionId, seed.RoundId, "M"), ct);
        }

        // Assert
        competitor.Made.Should().BeTrue();
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var verify = _fixture.CreateContext();
        var session = await PokerSeed.Load(verify, seed.SessionId, ct);
        session.Members.Should().ContainSingle(m => m.EmployeeId == seed.Voter.EmployeeId);
        session.Rounds.Single().Votes.Should().ContainSingle()
            .Which.Should().Match<PokerVote>(v => v.ParticipantId == seed.Voter.UserId && v.Value == "M");
        notifier.Verify(n => n.NotifyVoteSubmitted(seed.SessionId, seed.RoundId, seed.Voter.UserId, "Ada Lovelace"), Times.Once);
        notifier.Verify(n => n.NotifyMemberUpdated(seed.SessionId, It.IsAny<Application.PokerSessions.Dtos.PokerSessionMemberDto>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTurningVotingOffSavedFirst_RefusesTheVote()
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
            var set = await new SetPokerSessionVotingCommandHandler(other, access.Object, Mock.Of<IPokerSessionNotifier>())
                .Handle(new SetPokerSessionVotingCommand(seed.SessionId, false), token);
            set.IsSuccess.Should().BeTrue(set.IsFailure ? set.Error : null);
        });

        // Act
        CSharpFunctionalExtensions.Result result;
        await using (var context = _fixture.CreateContext(competitor))
        {
            result = await new SubmitVoteCommandHandler(context, access.Object, SqlServerDbContextFixture.DateTimeProvider(), Mock.Of<ICurrentUser>(), notifier.Object)
                .Handle(new SubmitVoteCommand(seed.SessionId, seed.RoundId, "M"), ct);
        }

        // Assert
        competitor.Made.Should().BeTrue();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not voting");
        await using var verify = _fixture.CreateContext();
        var saved = await PokerSeed.Load(verify, seed.SessionId, ct);
        saved.Members.Should().ContainSingle().Which.IsVoting.Should().BeFalse();
        saved.Rounds.Single().Votes.Should().BeEmpty();
        notifier.VerifyNoOtherCalls();
    }
}
