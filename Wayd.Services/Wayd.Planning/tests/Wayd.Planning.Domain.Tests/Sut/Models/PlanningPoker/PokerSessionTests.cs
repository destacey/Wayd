using Wayd.Common.Domain.Events;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.Domain.Tests.Data;
using NodaTime.Extensions;

namespace Wayd.Planning.Domain.Tests.Sut.Models.PlanningPoker;

public class PokerSessionTests
{
    private static readonly Instant Now = DateTime.UtcNow.ToInstant();

    private static readonly EstimationScale Scale = new EstimationScaleFaker().WithValues("1", "2", "3", "5", "8", "13", "?", "☕").Generate();

    private static PokerSession CreateActiveSession()
    {
        return PokerSession.Create("Sprint 1 Refinement", Scale, Guid.NewGuid().ToString(), Now).Value;
    }

    #region Create

    [Fact]
    public void Create_ValidParameters_ShouldReturnActiveSession()
    {
        // Arrange
        var facilitatorId = Guid.NewGuid().ToString();

        // Act
        var result = PokerSession.Create("Sprint 1 Refinement", Scale, facilitatorId, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Sprint 1 Refinement");
        result.Value.EstimationScaleId.Should().Be(Scale.Id);
        result.Value.EstimationScaleVersionId.Should().Be(Scale.CurrentVersion.Id);
        result.Value.FacilitatorId.Should().Be(facilitatorId);
        result.Value.Status.Should().Be(PokerSessionStatus.Active);
        result.Value.ActivatedOn.Should().Be(Now);
        result.Value.CompletedOn.Should().BeNull();
        result.Value.Rounds.Should().BeEmpty();
    }

    [Fact]
    public void Create_WhitespaceName_ShouldReturnFailure()
    {
        // Act
        var result = PokerSession.Create("  ", Scale, Guid.NewGuid().ToString(), Now);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_InactiveScale_ShouldReturnFailure()
    {
        // Arrange
        var inactive = new EstimationScaleFaker().WithIsActive(false).Generate();

        // Act
        var result = PokerSession.Create("Sprint 1 Refinement", inactive, Guid.NewGuid().ToString(), Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The estimation scale is inactive and cannot be chosen for a session.");
    }

    [Fact]
    public void Create_ScaleWithSeveralVersions_ShouldPinTheCurrentVersion()
    {
        // Arrange
        var scale = new EstimationScaleFaker().WithValues("1", "2", "3").Generate();
        scale.PublishVersion([new("1", true), new("2", true), new("5", true)], EventActor.System, Now);

        // Act
        var result = PokerSession.Create("Refinement", scale, Guid.NewGuid().ToString(), Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EstimationScaleVersionId.Should().Be(scale.Versions.Single(v => v.Number == 2).Id);
    }

    #endregion

    #region Complete

    [Fact]
    public void Complete_FromActive_ShouldReturnSuccess()
    {
        // Arrange
        var session = CreateActiveSession();

        // Act
        var result = session.Complete(Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.Status.Should().Be(PokerSessionStatus.Completed);
        session.CompletedOn.Should().Be(Now);
    }

    [Fact]
    public void Complete_FromCompleted_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        session.Complete(Now);

        // Act
        var result = session.Complete(Now);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region AddRound

    [Fact]
    public void AddRound_ActiveSession_ShouldReturnVotingRound()
    {
        // Arrange
        var session = CreateActiveSession();

        // Act
        var result = session.AddRound("WI-123: Implement login page");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Label.Should().Be("WI-123: Implement login page");
        result.Value.Status.Should().Be(PokerRoundStatus.Voting);
        result.Value.Order.Should().Be(0);
        session.Rounds.Should().HaveCount(1);
    }

    [Fact]
    public void AddRound_CompletedSession_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        session.Complete(Now);

        // Act
        var result = session.AddRound("WI-1: Test");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddRound_MultipleRounds_ShouldIncrementOrder()
    {
        // Arrange
        var session = CreateActiveSession();

        // Act
        var round1 = session.AddRound("WI-1: First").Value;
        var round2 = session.AddRound("WI-2: Second").Value;
        var round3 = session.AddRound("WI-3: Third").Value;

        // Assert
        round1.Order.Should().Be(0);
        round2.Order.Should().Be(1);
        round3.Order.Should().Be(2);
    }

    #endregion

    #region RemoveRound

    [Fact]
    public void RemoveRound_ExistingRound_ShouldReturnSuccess()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test").Value;

        // Act
        var result = session.RemoveRound(round.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.Rounds.Should().BeEmpty();
    }

    [Fact]
    public void RemoveRound_NonExistentRound_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();

        // Act
        var result = session.RemoveRound(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    #endregion

    #region Vote

    [Fact]
    public void SubmitVote_VotingRound_ShouldReturnSuccess()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test").Value;
        var participantId = Guid.NewGuid().ToString();

        // Act
        var result = session.SubmitVote(round.Id, participantId, "5", Scale.CurrentVersion, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        round.Votes.Should().HaveCount(1);
        round.Votes.First().ParticipantId.Should().Be(participantId);
        round.Votes.First().Value.Should().Be("5");
    }

    [Fact]
    public void SubmitVote_SameParticipantTwice_ShouldUpdateExistingVote()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test").Value;
        var participantId = Guid.NewGuid().ToString();
        session.SubmitVote(round.Id, participantId, "3", Scale.CurrentVersion, Now);

        // Act
        var laterTimestamp = Now.Plus(Duration.FromMinutes(1));
        var result = session.SubmitVote(round.Id, participantId, "8", Scale.CurrentVersion, laterTimestamp);

        // Assert
        result.IsSuccess.Should().BeTrue();
        round.Votes.Should().HaveCount(1);
        round.Votes.First().Value.Should().Be("8");
        round.Votes.First().SubmittedOn.Should().Be(laterTimestamp);
    }

    [Fact]
    public void SubmitVote_RevealedRound_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test").Value;
        session.SubmitVote(round.Id, Guid.NewGuid().ToString(), "5", Scale.CurrentVersion, Now);
        session.RevealRound(round.Id);

        // Act
        var result = session.SubmitVote(round.Id, Guid.NewGuid().ToString(), "5", Scale.CurrentVersion, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("4")]
    [InlineData("coffee")]
    [InlineData("13.0")]
    public void SubmitVote_ValueNotOnTheScale_ShouldReturnFailure(string value)
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test").Value;

        // Act
        var result = session.SubmitVote(round.Id, Guid.NewGuid().ToString(), value, Scale.CurrentVersion, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("is not a value on the session's estimation scale");
        round.Votes.Should().BeEmpty();
    }

    [Theory]
    [InlineData("?")]
    [InlineData("☕")]
    public void SubmitVote_SpecialCardOnTheScale_ShouldReturnSuccess(string value)
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test").Value;

        // Act
        var result = session.SubmitVote(round.Id, Guid.NewGuid().ToString(), value, Scale.CurrentVersion, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        round.Votes.Should().ContainSingle().Which.Value.Should().Be(value);
    }

    [Fact]
    public void SubmitVote_ScaleDeactivatedSinceCreation_StillAcceptsItsValues()
    {
        // Arrange
        var scale = new EstimationScaleFaker().WithValues("S", "M", "L").Generate();
        var session = PokerSession.Create("Refinement", scale, Guid.NewGuid().ToString(), Now).Value;
        var round = session.AddRound(null).Value;
        scale.Deactivate(EventActor.System, Now);

        // Act
        var result = session.SubmitVote(round.Id, Guid.NewGuid().ToString(), "M", scale.CurrentVersion, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        round.Votes.Should().ContainSingle().Which.Value.Should().Be("M");
    }

    [Fact]
    public void SubmitVote_AfterTheScalePublishesANewVersion_ShouldStillVoteOnThePinnedVersion()
    {
        // Arrange
        var scale = new EstimationScaleFaker().WithValues("S", "M", "L").Generate();
        var session = PokerSession.Create("Refinement", scale, Guid.NewGuid().ToString(), Now).Value;
        var pinned = scale.CurrentVersion;
        var round = session.AddRound(null).Value;
        scale.PublishVersion([new("Small", true), new("Medium", true), new("Large", true)], EventActor.System, Now);

        // Act
        var oldValue = session.SubmitVote(round.Id, Guid.NewGuid().ToString(), "M", pinned, Now);
        var newValue = session.SubmitVote(round.Id, Guid.NewGuid().ToString(), "Medium", pinned, Now);

        // Assert
        oldValue.IsSuccess.Should().BeTrue();
        newValue.IsFailure.Should().BeTrue();
        round.Votes.Should().ContainSingle().Which.Value.Should().Be("M");
    }

    [Fact]
    public void SubmitVote_AnotherVersion_ShouldThrow()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound(null).Value;
        var other = new EstimationScaleFaker().Generate().CurrentVersion;

        // Act
        var act = () => session.SubmitVote(round.Id, Guid.NewGuid().ToString(), "5", other, Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SubmitVote_ValueWithSurroundingWhitespace_ShouldRecordTheScaleValue()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound(null).Value;

        // Act
        var result = session.SubmitVote(round.Id, Guid.NewGuid().ToString(), " 8 ", Scale.CurrentVersion, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        round.Votes.Should().ContainSingle().Which.Value.Should().Be("8");
    }

    #endregion

    #region WithdrawVote

    [Fact]
    public void WithdrawVote_ExistingVote_ShouldRemoveVote()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test").Value;
        var participantId = Guid.NewGuid().ToString();
        session.SubmitVote(round.Id, participantId, "5", Scale.CurrentVersion, Now);

        // Act
        var result = session.WithdrawVote(round.Id, participantId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        round.Votes.Should().BeEmpty();
    }

    [Fact]
    public void WithdrawVote_NoVote_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test").Value;

        // Act
        var result = session.WithdrawVote(round.Id, Guid.NewGuid().ToString());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No vote found");
    }

    [Fact]
    public void WithdrawVote_RevealedRound_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test").Value;
        var participantId = Guid.NewGuid().ToString();
        session.SubmitVote(round.Id, participantId, "5", Scale.CurrentVersion, Now);
        session.RevealRound(round.Id);

        // Act
        var result = session.WithdrawVote(round.Id, participantId);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void WithdrawVote_CompletedSession_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test").Value;
        var participantId = Guid.NewGuid().ToString();
        session.SubmitVote(round.Id, participantId, "5", Scale.CurrentVersion, Now);
        session.Complete(Now);

        // Act
        var result = session.WithdrawVote(round.Id, participantId);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region RevealRound

    [Fact]
    public void RevealRound_VotingRound_ShouldReturnSuccess()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test").Value;
        session.SubmitVote(round.Id, Guid.NewGuid().ToString(), "5", Scale.CurrentVersion, Now);

        // Act
        var result = session.RevealRound(round.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(PokerRoundStatus.Revealed);
    }

    [Fact]
    public void RevealRound_AcceptedRound_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test").Value;
        session.SubmitVote(round.Id, Guid.NewGuid().ToString(), "5", Scale.CurrentVersion, Now);
        session.RevealRound(round.Id);
        session.SetConsensus(round.Id, "5", Scale.CurrentVersion);

        // Act
        var result = session.RevealRound(round.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region ResetRound

    [Fact]
    public void ResetRound_RevealedRound_ShouldClearVotesAndReturnToVoting()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test").Value;
        session.SubmitVote(round.Id, Guid.NewGuid().ToString(), "5", Scale.CurrentVersion, Now);
        session.RevealRound(round.Id);

        // Act
        var result = session.ResetRound(round.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(PokerRoundStatus.Voting);
        result.Value.Votes.Should().BeEmpty();
    }

    [Fact]
    public void ResetRound_AcceptedRound_ShouldClearVotesAndConsensusAndReturnToVoting()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test").Value;
        session.SubmitVote(round.Id, Guid.NewGuid().ToString(), "5", Scale.CurrentVersion, Now);
        session.RevealRound(round.Id);
        session.SetConsensus(round.Id, "5", Scale.CurrentVersion);

        // Act
        var result = session.ResetRound(round.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(PokerRoundStatus.Voting);
        result.Value.Votes.Should().BeEmpty();
        result.Value.ConsensusEstimate.Should().BeNull();
    }

    #endregion

    #region SetConsensus

    [Fact]
    public void SetConsensus_RevealedRound_ShouldReturnSuccess()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test").Value;
        session.SubmitVote(round.Id, Guid.NewGuid().ToString(), "5", Scale.CurrentVersion, Now);
        session.RevealRound(round.Id);

        // Act
        var result = session.SetConsensus(round.Id, "5", Scale.CurrentVersion);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ConsensusEstimate.Should().Be("5");
        result.Value.Status.Should().Be(PokerRoundStatus.Accepted);
    }

    [Theory]
    [InlineData("4")]
    [InlineData("anything")]
    public void SetConsensus_ValueNotOnTheVersion_ShouldReturnFailure(string estimate)
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound(null).Value;
        session.SubmitVote(round.Id, Guid.NewGuid().ToString(), "5", Scale.CurrentVersion, Now);
        session.RevealRound(round.Id);

        // Act
        var result = session.SetConsensus(round.Id, estimate, Scale.CurrentVersion);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("is not a value on the session's estimation scale");
        round.Status.Should().Be(PokerRoundStatus.Revealed);
        round.ConsensusEstimate.Should().BeNull();
    }

    [Fact]
    public void SetConsensus_ACard_ShouldBeAccepted()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound(null).Value;
        session.SubmitVote(round.Id, Guid.NewGuid().ToString(), "?", Scale.CurrentVersion, Now);
        session.RevealRound(round.Id);

        // Act
        var result = session.SetConsensus(round.Id, " ? ", Scale.CurrentVersion);

        // Assert
        result.IsSuccess.Should().BeTrue();
        round.ConsensusEstimate.Should().Be("?");
        round.Status.Should().Be(PokerRoundStatus.Accepted);
    }

    [Fact]
    public void SetConsensus_AnotherVersion_ShouldThrow()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound(null).Value;
        var other = new EstimationScaleFaker().Generate().CurrentVersion;

        // Act
        var act = () => session.SetConsensus(round.Id, "5", other);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetConsensus_VotingRound_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test").Value;

        // Act
        var result = session.SetConsensus(round.Id, "5", Scale.CurrentVersion);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("revealed");
    }

    #endregion

    #region Update

    [Fact]
    public void Update_ToAnInactiveScale_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        var inactive = new EstimationScaleFaker().WithIsActive(false).Generate();

        // Act
        var result = session.Update("Renamed", inactive);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The estimation scale is inactive and cannot be chosen for a session.");
        session.EstimationScaleId.Should().Be(Scale.Id);
        session.EstimationScaleVersionId.Should().Be(Scale.CurrentVersion.Id);
        session.Name.Should().Be("Sprint 1 Refinement");
    }

    [Fact]
    public void Update_KeepingAScaleDeactivatedSinceCreation_ShouldReturnSuccess()
    {
        // Arrange
        var scale = new EstimationScaleFaker().Generate();
        var session = PokerSession.Create("Refinement", scale, Guid.NewGuid().ToString(), Now).Value;
        scale.Deactivate(EventActor.System, Now);

        // Act
        var result = session.Update("Renamed", scale);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.Name.Should().Be("Renamed");
        session.EstimationScaleId.Should().Be(scale.Id);
    }

    [Fact]
    public void Update_ToAnotherActiveScale_ShouldPinItsCurrentVersion()
    {
        // Arrange
        var session = CreateActiveSession();
        var other = new EstimationScaleFaker().WithValues("S", "M", "L").Generate();
        other.PublishVersion([new("S", true), new("M", true), new("L", true), new("XL", true)], EventActor.System, Now);

        // Act
        var result = session.Update("Renamed", other);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.EstimationScaleId.Should().Be(other.Id);
        session.EstimationScaleVersionId.Should().Be(other.Versions.Single(v => v.Number == 2).Id);
    }

    [Fact]
    public void Update_KeepingTheScale_ShouldKeepThePinnedVersion()
    {
        // Arrange
        var scale = new EstimationScaleFaker().WithValues("S", "M", "L").Generate();
        var session = PokerSession.Create("Refinement", scale, Guid.NewGuid().ToString(), Now).Value;
        var pinned = scale.CurrentVersion.Id;
        scale.PublishVersion([new("S", true), new("M", true), new("L", true), new("XL", true)], EventActor.System, Now);

        // Act
        var result = session.Update("Renamed", scale);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.EstimationScaleVersionId.Should().Be(pinned);
    }

    [Fact]
    public void Update_ToAnotherScaleAfterRoundsStarted_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        session.AddRound(null);
        var other = new EstimationScaleFaker().Generate();

        // Act
        var result = session.Update("Renamed", other);

        // Assert
        result.IsFailure.Should().BeTrue();
        session.EstimationScaleId.Should().Be(Scale.Id);
        session.EstimationScaleVersionId.Should().Be(Scale.CurrentVersion.Id);
    }

    #endregion
}
