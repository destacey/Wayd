using Wayd.Common.Domain.Enums;
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
        return PokerSession.Create("Sprint 1 Refinement", Scale, Visibility.Public, PokerSessionActors.FacilitatorEmployeeId, Now).Value;
    }

    #region Create

    [Fact]
    public void Create_ValidParameters_ShouldReturnActiveSession()
    {
        // Arrange
        var facilitatorId = Guid.NewGuid();

        // Act
        var result = PokerSession.Create("Sprint 1 Refinement", Scale, Visibility.Private, facilitatorId, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Sprint 1 Refinement");
        result.Value.EstimationScaleId.Should().Be(Scale.Id);
        result.Value.EstimationScaleVersionId.Should().Be(Scale.CurrentVersion.Id);
        result.Value.Facilitators.Should().ContainSingle().Which.EmployeeId.Should().Be(facilitatorId);
        result.Value.Visibility.Should().Be(Visibility.Private);
        result.Value.Members.Should().BeEmpty();
        result.Value.Status.Should().Be(PokerSessionStatus.Active);
        result.Value.ActivatedOn.Should().Be(Now);
        result.Value.CompletedOn.Should().BeNull();
        result.Value.Rounds.Should().BeEmpty();
    }

    [Fact]
    public void Create_WhitespaceName_ShouldReturnFailure()
    {
        // Act
        var result = PokerSession.Create("  ", Scale, Visibility.Public, PokerSessionActors.FacilitatorEmployeeId, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_InactiveScale_ShouldReturnFailure()
    {
        // Arrange
        var inactive = new EstimationScaleFaker().WithIsActive(false).Generate();

        // Act
        var result = PokerSession.Create("Sprint 1 Refinement", inactive, Visibility.Public, PokerSessionActors.FacilitatorEmployeeId, Now);

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
        var result = PokerSession.Create("Refinement", scale, Visibility.Public, PokerSessionActors.FacilitatorEmployeeId, Now);

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
        var result = session.Complete(PokerSessionActors.Facilitator, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.Status.Should().Be(PokerSessionStatus.Completed);
        session.CompletedOn.Should().Be(Now);
    }

    [Fact]
    public void Complete_ShouldDeleteEmptyRoundsAndKeepTheRest()
    {
        // Arrange
        var session = CreateActiveSession();
        var labelled = session.AddRound("WI-1", null, PokerSessionActors.Facilitator).Value;
        var described = session.AddRound("", "Only a description", PokerSessionActors.Facilitator).Value;
        var voted = session.AddRound(null, null, PokerSessionActors.Facilitator).Value;
        session.SubmitVote(voted.Id, "5", Scale.CurrentVersion, PokerSessionActors.Voter("user-1"), Now);
        session.AddRound("", null, PokerSessionActors.Facilitator);
        session.AddRound("   ", null, PokerSessionActors.Facilitator);
        session.AddRound(null, null, PokerSessionActors.Facilitator);

        // Act
        var result = session.Complete(PokerSessionActors.Facilitator, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.Rounds.Should().BeEquivalentTo([labelled, described, voted]);
    }

    [Fact]
    public void Complete_FromCompleted_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        session.Complete(PokerSessionActors.Facilitator, Now);

        // Act
        var result = session.Complete(PokerSessionActors.Facilitator, Now);

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
        var result = session.AddRound("WI-123: Implement login page", null, PokerSessionActors.Facilitator);

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
        session.Complete(PokerSessionActors.Facilitator, Now);

        // Act
        var result = session.AddRound("WI-1: Test", null, PokerSessionActors.Facilitator);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddRound_MultipleRounds_ShouldIncrementOrder()
    {
        // Arrange
        var session = CreateActiveSession();

        // Act
        var round1 = session.AddRound("WI-1: First", null, PokerSessionActors.Facilitator).Value;
        var round2 = session.AddRound("WI-2: Second", null, PokerSessionActors.Facilitator).Value;
        var round3 = session.AddRound("WI-3: Third", null, PokerSessionActors.Facilitator).Value;

        // Assert
        round1.Order.Should().Be(0);
        round2.Order.Should().Be(1);
        round3.Order.Should().Be(2);
    }

    [Fact]
    public void AddRound_WithDescription_ShouldStoreTrimmedDescription()
    {
        // Arrange
        var session = CreateActiveSession();

        // Act
        var result = session.AddRound("WI-1: Test", "  Users can sign in with SSO.  ", PokerSessionActors.Facilitator);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Be("Users can sign in with SSO.");
    }

    [Fact]
    public void AddRound_WithBlankDescription_ShouldStoreNoDescription()
    {
        // Arrange
        var session = CreateActiveSession();

        // Act
        var result = session.AddRound("WI-1: Test", "   ", PokerSessionActors.Facilitator);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().BeNull();
    }

    #endregion

    #region UpdateRoundDescription

    [Fact]
    public void UpdateRoundDescription_ActiveSession_ShouldReplaceDescription()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test", "Original", PokerSessionActors.Facilitator).Value;

        // Act
        var result = session.UpdateRoundDescription(round.Id, " Updated ", PokerSessionActors.Facilitator);

        // Assert
        result.IsSuccess.Should().BeTrue();
        round.Description.Should().Be("Updated");
        round.Label.Should().Be("WI-1: Test");
    }

    [Fact]
    public void UpdateRoundDescription_BlankDescription_ShouldClearIt()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test", "Original", PokerSessionActors.Facilitator).Value;

        // Act
        var result = session.UpdateRoundDescription(round.Id, " ", PokerSessionActors.Facilitator);

        // Assert
        result.IsSuccess.Should().BeTrue();
        round.Description.Should().BeNull();
    }

    [Fact]
    public void UpdateRoundDescription_CompletedSession_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test", null, PokerSessionActors.Facilitator).Value;
        session.Complete(PokerSessionActors.Facilitator, Now);

        // Act
        var result = session.UpdateRoundDescription(round.Id, "Updated", PokerSessionActors.Facilitator);

        // Assert
        result.IsFailure.Should().BeTrue();
        round.Description.Should().BeNull();
    }

    [Fact]
    public void UpdateRoundDescription_UnknownRound_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();

        // Act
        var result = session.UpdateRoundDescription(Guid.NewGuid(), "Updated", PokerSessionActors.Facilitator);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region RemoveRound

    [Fact]
    public void RemoveRound_ExistingRound_ShouldReturnSuccess()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test", null, PokerSessionActors.Facilitator).Value;

        // Act
        var result = session.RemoveRound(round.Id, PokerSessionActors.Facilitator);

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
        var result = session.RemoveRound(Guid.NewGuid(), PokerSessionActors.Facilitator);

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
        var round = session.AddRound("WI-1: Test", null, PokerSessionActors.Facilitator).Value;
        var participantId = Guid.NewGuid().ToString();

        // Act
        var result = session.SubmitVote(round.Id, "5", Scale.CurrentVersion, PokerSessionActors.Voter(participantId), Now);

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
        var round = session.AddRound("WI-1: Test", null, PokerSessionActors.Facilitator).Value;
        var participantId = Guid.NewGuid().ToString();
        session.SubmitVote(round.Id, "3", Scale.CurrentVersion, PokerSessionActors.Voter(participantId), Now);

        // Act
        var laterTimestamp = Now.Plus(Duration.FromMinutes(1));
        var result = session.SubmitVote(round.Id, "8", Scale.CurrentVersion, PokerSessionActors.Voter(participantId), laterTimestamp);

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
        var round = session.AddRound("WI-1: Test", null, PokerSessionActors.Facilitator).Value;
        session.SubmitVote(round.Id, "5", Scale.CurrentVersion, PokerSessionActors.Voter(Guid.NewGuid().ToString()), Now);
        session.RevealRound(round.Id, PokerSessionActors.Facilitator);

        // Act
        var result = session.SubmitVote(round.Id, "5", Scale.CurrentVersion, PokerSessionActors.Voter(Guid.NewGuid().ToString()), Now);

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
        var round = session.AddRound("WI-1: Test", null, PokerSessionActors.Facilitator).Value;

        // Act
        var result = session.SubmitVote(round.Id, value, Scale.CurrentVersion, PokerSessionActors.Voter(Guid.NewGuid().ToString()), Now);

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
        var round = session.AddRound("WI-1: Test", null, PokerSessionActors.Facilitator).Value;

        // Act
        var result = session.SubmitVote(round.Id, value, Scale.CurrentVersion, PokerSessionActors.Voter(Guid.NewGuid().ToString()), Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        round.Votes.Should().ContainSingle().Which.Value.Should().Be(value);
    }

    [Fact]
    public void SubmitVote_ScaleDeactivatedSinceCreation_StillAcceptsItsValues()
    {
        // Arrange
        var scale = new EstimationScaleFaker().WithValues("S", "M", "L").Generate();
        var session = PokerSession.Create("Refinement", scale, Visibility.Public, PokerSessionActors.FacilitatorEmployeeId, Now).Value;
        var round = session.AddRound(null, null, PokerSessionActors.Facilitator).Value;
        scale.Deactivate(EventActor.System, Now);

        // Act
        var result = session.SubmitVote(round.Id, "M", scale.CurrentVersion, PokerSessionActors.Voter(Guid.NewGuid().ToString()), Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        round.Votes.Should().ContainSingle().Which.Value.Should().Be("M");
    }

    [Fact]
    public void SubmitVote_AfterTheScalePublishesANewVersion_ShouldStillVoteOnThePinnedVersion()
    {
        // Arrange
        var scale = new EstimationScaleFaker().WithValues("S", "M", "L").Generate();
        var session = PokerSession.Create("Refinement", scale, Visibility.Public, PokerSessionActors.FacilitatorEmployeeId, Now).Value;
        var pinned = scale.CurrentVersion;
        var round = session.AddRound(null, null, PokerSessionActors.Facilitator).Value;
        scale.PublishVersion([new("Small", true), new("Medium", true), new("Large", true)], EventActor.System, Now);

        // Act
        var oldValue = session.SubmitVote(round.Id, "M", pinned, PokerSessionActors.Voter(Guid.NewGuid().ToString()), Now);
        var newValue = session.SubmitVote(round.Id, "Medium", pinned, PokerSessionActors.Voter(Guid.NewGuid().ToString()), Now);

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
        var round = session.AddRound(null, null, PokerSessionActors.Facilitator).Value;
        var other = new EstimationScaleFaker().Generate().CurrentVersion;

        // Act
        var act = () => session.SubmitVote(round.Id, "5", other, PokerSessionActors.Voter(Guid.NewGuid().ToString()), Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SubmitVote_ValueWithSurroundingWhitespace_ShouldRecordTheScaleValue()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound(null, null, PokerSessionActors.Facilitator).Value;

        // Act
        var result = session.SubmitVote(round.Id, " 8 ", Scale.CurrentVersion, PokerSessionActors.Voter(Guid.NewGuid().ToString()), Now);

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
        var round = session.AddRound("WI-1: Test", null, PokerSessionActors.Facilitator).Value;
        var participantId = Guid.NewGuid().ToString();
        session.SubmitVote(round.Id, "5", Scale.CurrentVersion, PokerSessionActors.Voter(participantId), Now);

        // Act
        var result = session.WithdrawVote(round.Id, PokerSessionActors.Voter(participantId));

        // Assert
        result.IsSuccess.Should().BeTrue();
        round.Votes.Should().BeEmpty();
    }

    [Fact]
    public void WithdrawVote_NoVote_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test", null, PokerSessionActors.Facilitator).Value;

        // Act
        var result = session.WithdrawVote(round.Id, PokerSessionActors.Voter(Guid.NewGuid().ToString()));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No vote found");
    }

    [Fact]
    public void WithdrawVote_RevealedRound_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test", null, PokerSessionActors.Facilitator).Value;
        var participantId = Guid.NewGuid().ToString();
        session.SubmitVote(round.Id, "5", Scale.CurrentVersion, PokerSessionActors.Voter(participantId), Now);
        session.RevealRound(round.Id, PokerSessionActors.Facilitator);

        // Act
        var result = session.WithdrawVote(round.Id, PokerSessionActors.Voter(participantId));

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void WithdrawVote_CompletedSession_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test", null, PokerSessionActors.Facilitator).Value;
        var participantId = Guid.NewGuid().ToString();
        session.SubmitVote(round.Id, "5", Scale.CurrentVersion, PokerSessionActors.Voter(participantId), Now);
        session.Complete(PokerSessionActors.Facilitator, Now);

        // Act
        var result = session.WithdrawVote(round.Id, PokerSessionActors.Voter(participantId));

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
        var round = session.AddRound("WI-1: Test", null, PokerSessionActors.Facilitator).Value;
        session.SubmitVote(round.Id, "5", Scale.CurrentVersion, PokerSessionActors.Voter(Guid.NewGuid().ToString()), Now);

        // Act
        var result = session.RevealRound(round.Id, PokerSessionActors.Facilitator);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(PokerRoundStatus.Revealed);
    }

    [Fact]
    public void RevealRound_AcceptedRound_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test", null, PokerSessionActors.Facilitator).Value;
        session.SubmitVote(round.Id, "5", Scale.CurrentVersion, PokerSessionActors.Voter(Guid.NewGuid().ToString()), Now);
        session.RevealRound(round.Id, PokerSessionActors.Facilitator);
        session.SetConsensus(round.Id, "5", Scale.CurrentVersion, PokerSessionActors.Facilitator);

        // Act
        var result = session.RevealRound(round.Id, PokerSessionActors.Facilitator);

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
        var round = session.AddRound("WI-1: Test", null, PokerSessionActors.Facilitator).Value;
        session.SubmitVote(round.Id, "5", Scale.CurrentVersion, PokerSessionActors.Voter(Guid.NewGuid().ToString()), Now);
        session.RevealRound(round.Id, PokerSessionActors.Facilitator);

        // Act
        var result = session.ResetRound(round.Id, PokerSessionActors.Facilitator);

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
        var round = session.AddRound("WI-1: Test", null, PokerSessionActors.Facilitator).Value;
        session.SubmitVote(round.Id, "5", Scale.CurrentVersion, PokerSessionActors.Voter(Guid.NewGuid().ToString()), Now);
        session.RevealRound(round.Id, PokerSessionActors.Facilitator);
        session.SetConsensus(round.Id, "5", Scale.CurrentVersion, PokerSessionActors.Facilitator);

        // Act
        var result = session.ResetRound(round.Id, PokerSessionActors.Facilitator);

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
        var round = session.AddRound("WI-1: Test", null, PokerSessionActors.Facilitator).Value;
        session.SubmitVote(round.Id, "5", Scale.CurrentVersion, PokerSessionActors.Voter(Guid.NewGuid().ToString()), Now);
        session.RevealRound(round.Id, PokerSessionActors.Facilitator);

        // Act
        var result = session.SetConsensus(round.Id, "5", Scale.CurrentVersion, PokerSessionActors.Facilitator);

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
        var round = session.AddRound(null, null, PokerSessionActors.Facilitator).Value;
        session.SubmitVote(round.Id, "5", Scale.CurrentVersion, PokerSessionActors.Voter(Guid.NewGuid().ToString()), Now);
        session.RevealRound(round.Id, PokerSessionActors.Facilitator);

        // Act
        var result = session.SetConsensus(round.Id, estimate, Scale.CurrentVersion, PokerSessionActors.Facilitator);

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
        var round = session.AddRound(null, null, PokerSessionActors.Facilitator).Value;
        session.SubmitVote(round.Id, "?", Scale.CurrentVersion, PokerSessionActors.Voter(Guid.NewGuid().ToString()), Now);
        session.RevealRound(round.Id, PokerSessionActors.Facilitator);

        // Act
        var result = session.SetConsensus(round.Id, " ? ", Scale.CurrentVersion, PokerSessionActors.Facilitator);

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
        var round = session.AddRound(null, null, PokerSessionActors.Facilitator).Value;
        var other = new EstimationScaleFaker().Generate().CurrentVersion;

        // Act
        var act = () => session.SetConsensus(round.Id, "5", other, PokerSessionActors.Facilitator);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetConsensus_VotingRound_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test", null, PokerSessionActors.Facilitator).Value;

        // Act
        var result = session.SetConsensus(round.Id, "5", Scale.CurrentVersion, PokerSessionActors.Facilitator);

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
        var result = session.Update("Renamed", inactive, PokerSessionActors.Facilitator);

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
        var session = PokerSession.Create("Refinement", scale, Visibility.Public, PokerSessionActors.FacilitatorEmployeeId, Now).Value;
        scale.Deactivate(EventActor.System, Now);

        // Act
        var result = session.Update("Renamed", scale, PokerSessionActors.Facilitator);

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
        var result = session.Update("Renamed", other, PokerSessionActors.Facilitator);

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
        var session = PokerSession.Create("Refinement", scale, Visibility.Public, PokerSessionActors.FacilitatorEmployeeId, Now).Value;
        var pinned = scale.CurrentVersion.Id;
        scale.PublishVersion([new("S", true), new("M", true), new("L", true), new("XL", true)], EventActor.System, Now);

        // Act
        var result = session.Update("Renamed", scale, PokerSessionActors.Facilitator);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.EstimationScaleVersionId.Should().Be(pinned);
    }

    [Fact]
    public void Update_ToAnotherScaleAfterRoundsStarted_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        session.AddRound(null, null, PokerSessionActors.Facilitator);
        var other = new EstimationScaleFaker().Generate();

        // Act
        var result = session.Update("Renamed", other, PokerSessionActors.Facilitator);

        // Assert
        result.IsFailure.Should().BeTrue();
        session.EstimationScaleId.Should().Be(Scale.Id);
        session.EstimationScaleVersionId.Should().Be(Scale.CurrentVersion.Id);
    }

    #endregion

    #region Access

    private static readonly Guid InvitedEmployeeId = Guid.Parse("7a1f0c00-0000-4000-a000-0000000e0001");
    private static readonly Guid InvitedTeamId = Guid.Parse("7a1f0c00-0000-4000-a000-0000000b0001");

    private static PokerSession CreatePrivateSession() =>
        new PokerSessionFaker()
            .WithEstimationScale(Scale)
            .WithVisibility(Visibility.Private)
            .WithInvitedEmployees(InvitedEmployeeId)
            .WithInvitedTeams(InvitedTeamId)
            .Generate();

    [Fact]
    public void CanManage_ShouldAdmitFacilitatorsAndAdministratorsOnly()
    {
        // Arrange
        var session = CreateActiveSession();

        // Act & Assert
        session.CanManage(PokerSessionActors.Facilitator).Should().BeTrue();
        session.CanManage(PokerSessionActors.Administrator).Should().BeTrue();
        session.CanManage(PokerSessionActors.Voter("someone")).Should().BeFalse();
    }

    [Fact]
    public void CanAccess_PublicSession_ShouldAdmitAnyone()
    {
        // Arrange
        var session = CreateActiveSession();

        // Act
        var canAccess = session.CanAccess(PokerSessionActors.Voter("someone"));

        // Assert
        canAccess.Should().BeTrue();
    }

    [Fact]
    public void CanAccess_PrivateSession_ShouldAdmitOnlyThePeopleOnIt()
    {
        // Arrange
        var session = CreatePrivateSession();

        // Act & Assert
        session.CanAccess(PokerSessionActors.Facilitator).Should().BeTrue();
        session.CanAccess(PokerSessionActors.Administrator).Should().BeTrue();
        session.CanAccess(PokerSessionActors.Employee(InvitedEmployeeId)).Should().BeTrue();
        session.CanAccess(PokerSessionActors.Employee(Guid.NewGuid(), InvitedTeamId)).Should().BeTrue();
        session.CanAccess(PokerSessionActors.Employee(Guid.NewGuid(), Guid.NewGuid())).Should().BeFalse();
        session.CanAccess(PokerSessionActors.Voter("someone")).Should().BeFalse();
    }

    [Fact]
    public void AccessibleTo_ShouldAgreeWithCanAccess()
    {
        // Arrange
        PokerSession[] sessions = [CreateActiveSession(), CreatePrivateSession(), new PokerSessionFaker().WithVisibility(Visibility.Private).Generate()];
        PokerSessionActor[] actors =
        [
            PokerSessionActors.Facilitator,
            PokerSessionActors.Administrator,
            PokerSessionActors.Employee(InvitedEmployeeId),
            PokerSessionActors.Employee(Guid.NewGuid(), InvitedTeamId),
            PokerSessionActors.Voter("someone"),
        ];

        foreach (var actor in actors)
        {
            // Act
            var filter = PokerSession.AccessibleTo(actor.EmployeeId, actor.IsAdministrator, [.. actor.TeamIds]).Compile();

            // Assert
            foreach (var session in sessions)
                filter(session).Should().Be(session.CanAccess(actor), "the query filter and the aggregate must agree for {0}", actor.UserId);
        }
    }

    [Fact]
    public void AccessibleTo_ReaderWithoutEmployee_ShouldSeeOnlyPublicSessions()
    {
        // Arrange
        var filter = PokerSession.AccessibleTo(null, false, []).Compile();

        // Act & Assert
        filter(CreateActiveSession()).Should().BeTrue();
        filter(CreatePrivateSession()).Should().BeFalse();
    }

    [Fact]
    public void RevealRound_ByNonFacilitatorOnPublicSession_ShouldReturnNotAFacilitator()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("Story", null, PokerSessionActors.Facilitator).Value;

        // Act
        var result = session.RevealRound(round.Id, PokerSessionActors.Voter("someone"));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PokerSession.NotAFacilitatorError);
        round.Status.Should().Be(PokerRoundStatus.Voting);
    }

    [Fact]
    public void RevealRound_ByOutsiderOnPrivateSession_ShouldReadAsNotFound()
    {
        // Arrange
        var session = CreatePrivateSession();
        var round = session.AddRound("Story", null, PokerSessionActors.Facilitator).Value;

        // Act
        var result = session.RevealRound(round.Id, PokerSessionActors.Voter("someone"));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PokerSession.NotFoundError);
    }

    [Fact]
    public void RevealRound_ByAdministrator_ShouldSucceed()
    {
        // Arrange
        var session = CreatePrivateSession();
        var round = session.AddRound("Story", null, PokerSessionActors.Facilitator).Value;

        // Act
        var result = session.RevealRound(round.Id, PokerSessionActors.Administrator);

        // Assert
        result.IsSuccess.Should().BeTrue();
        round.Status.Should().Be(PokerRoundStatus.Revealed);
    }

    [Fact]
    public void CanDelete_ShouldAllowFacilitatorsOnly()
    {
        // Arrange
        var session = CreateActiveSession();

        // Act & Assert
        session.CanDelete(PokerSessionActors.Facilitator).IsSuccess.Should().BeTrue();
        session.CanDelete(PokerSessionActors.Voter("someone")).Error.Should().Be(PokerSession.NotAFacilitatorError);
    }

    [Fact]
    public void SetFacilitators_ShouldReplaceTheList()
    {
        // Arrange
        var session = CreateActiveSession();
        var other = Guid.NewGuid();

        // Act
        var result = session.SetFacilitators([other, other], PokerSessionActors.Facilitator);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.Facilitators.Select(f => f.EmployeeId).Should().Equal(other);
        session.CanManage(PokerSessionActors.Facilitator).Should().BeFalse();
    }

    [Fact]
    public void SetFacilitators_Empty_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();

        // Act
        var result = session.SetFacilitators([], PokerSessionActors.Facilitator);

        // Assert
        result.IsFailure.Should().BeTrue();
        session.Facilitators.Should().ContainSingle();
    }

    [Fact]
    public void SetFacilitators_ByNonFacilitator_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        var outsider = PokerSessionActors.Voter("someone");

        // Act
        var result = session.SetFacilitators([outsider.EmployeeId], outsider);

        // Assert
        result.Error.Should().Be(PokerSession.NotAFacilitatorError);
        session.IsFacilitator(outsider.EmployeeId).Should().BeFalse();
    }

    [Fact]
    public void SetInvitees_ShouldReplaceEmployeesAndTeams()
    {
        // Arrange
        var session = CreatePrivateSession();
        var employee = Guid.NewGuid();
        var team = Guid.NewGuid();

        // Act
        var result = session.SetInvitees([employee], [team], PokerSessionActors.Facilitator);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.InvitedEmployees.Select(e => e.EmployeeId).Should().Equal(employee);
        session.InvitedTeams.Select(t => t.TeamId).Should().Equal(team);
        session.CanAccess(PokerSessionActors.Employee(InvitedEmployeeId)).Should().BeFalse();
    }

    [Fact]
    public void SetVisibility_OnCompletedSession_ShouldSucceed()
    {
        // Arrange
        var session = CreateActiveSession();
        session.Complete(PokerSessionActors.Facilitator, Now);

        // Act
        var result = session.SetVisibility(Visibility.Private, PokerSessionActors.Facilitator);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.Visibility.Should().Be(Visibility.Private);
    }

    #endregion

    #region Members

    [Fact]
    public void Join_ShouldAddAParticipant()
    {
        // Arrange
        var session = CreateActiveSession();
        var actor = PokerSessionActors.Voter("user-1");

        // Act
        var result = session.Join(PokerSessionMemberMode.Participant, actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var member = session.Members.Should().ContainSingle().Subject;
        member.EmployeeId.Should().Be(actor.EmployeeId);
        member.Mode.Should().Be(PokerSessionMemberMode.Participant);
        member.JoinedOn.Should().Be(Now);
        member.LeftOn.Should().BeNull();
        result.Value.WithdrawnRoundIds.Should().BeEmpty();
    }

    [Fact]
    public void Join_PrivateSessionNotInvited_ShouldReadAsNotFound()
    {
        // Arrange
        var session = CreatePrivateSession();

        // Act
        var result = session.Join(PokerSessionMemberMode.Participant, PokerSessionActors.Voter("someone"), Now);

        // Assert
        result.Error.Should().Be(PokerSession.NotFoundError);
        session.Members.Should().BeEmpty();
    }

    [Fact]
    public void Join_PrivateSessionThroughATeam_ShouldSucceed()
    {
        // Arrange
        var session = CreatePrivateSession();

        // Act
        var result = session.Join(PokerSessionMemberMode.Observer, PokerSessionActors.Employee(Guid.NewGuid(), InvitedTeamId), Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Member.Mode.Should().Be(PokerSessionMemberMode.Observer);
    }

    [Fact]
    public void Join_CompletedSession_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        session.Complete(PokerSessionActors.Facilitator, Now);

        // Act
        var result = session.Join(PokerSessionMemberMode.Participant, PokerSessionActors.Voter("user-1"), Now);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Join_AgainAsObserver_ShouldWithdrawUnrevealedVotesAndKeepJoinTime()
    {
        // Arrange
        var session = CreateActiveSession();
        var actor = PokerSessionActors.Voter("user-1");
        var revealed = session.AddRound("Revealed", null, PokerSessionActors.Facilitator).Value;
        var voting = session.AddRound("Voting", null, PokerSessionActors.Facilitator).Value;
        session.SubmitVote(revealed.Id, "5", Scale.CurrentVersion, actor, Now);
        session.SubmitVote(voting.Id, "3", Scale.CurrentVersion, actor, Now);
        session.RevealRound(revealed.Id, PokerSessionActors.Facilitator);

        // Act
        var result = session.Join(PokerSessionMemberMode.Observer, actor, Now.Plus(Duration.FromMinutes(5)));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.WithdrawnRoundIds.Should().Equal(voting.Id);
        voting.Votes.Should().BeEmpty();
        revealed.Votes.Should().ContainSingle();
        result.Value.Member.Mode.Should().Be(PokerSessionMemberMode.Observer);
        result.Value.Member.JoinedOn.Should().Be(Now);
    }

    [Fact]
    public void ChangeMode_ToObserver_ShouldWithdrawUnrevealedVotesOnly()
    {
        // Arrange
        var session = CreateActiveSession();
        var actor = PokerSessionActors.Voter("user-1");
        var revealed = session.AddRound("Revealed", null, PokerSessionActors.Facilitator).Value;
        var voting = session.AddRound("Voting", null, PokerSessionActors.Facilitator).Value;
        session.SubmitVote(revealed.Id, "5", Scale.CurrentVersion, actor, Now);
        session.SubmitVote(voting.Id, "3", Scale.CurrentVersion, actor, Now);
        session.RevealRound(revealed.Id, PokerSessionActors.Facilitator);

        // Act
        var result = session.ChangeMode(PokerSessionMemberMode.Observer, actor);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.WithdrawnRoundIds.Should().Equal(voting.Id);
        voting.Votes.Should().BeEmpty();
        revealed.Votes.Should().ContainSingle();
        session.PresentMember(actor.EmployeeId)!.Mode.Should().Be(PokerSessionMemberMode.Observer);
    }

    [Fact]
    public void ChangeMode_ToParticipant_ShouldLetTheMemberVote()
    {
        // Arrange
        var session = CreateActiveSession();
        var actor = PokerSessionActors.Voter("user-1");
        var round = session.AddRound("Story", null, PokerSessionActors.Facilitator).Value;
        session.Join(PokerSessionMemberMode.Observer, actor, Now);

        // Act
        var result = session.ChangeMode(PokerSessionMemberMode.Participant, actor);
        var vote = session.SubmitVote(round.Id, "5", Scale.CurrentVersion, actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.WithdrawnRoundIds.Should().BeEmpty();
        vote.IsSuccess.Should().BeTrue();
        vote.Value.Should().BeFalse();
    }

    [Fact]
    public void ChangeMode_NotJoined_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();

        // Act
        var result = session.ChangeMode(PokerSessionMemberMode.Observer, PokerSessionActors.Voter("user-1"));

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Leave_ShouldKeepTheMemberStampedAndWithdrawUnrevealedVotes()
    {
        // Arrange
        var session = CreateActiveSession();
        var actor = PokerSessionActors.Voter("user-1");
        var round = session.AddRound("Story", null, PokerSessionActors.Facilitator).Value;
        session.SubmitVote(round.Id, "5", Scale.CurrentVersion, actor, Now);
        var leftOn = Now.Plus(Duration.FromMinutes(10));

        // Act
        var result = session.Leave(actor, leftOn);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.WithdrawnRoundIds.Should().Equal(round.Id);
        round.Votes.Should().BeEmpty();
        var member = session.Members.Should().ContainSingle().Subject;
        member.LeftOn.Should().Be(leftOn);
        member.Mode.Should().Be(PokerSessionMemberMode.Participant);
        session.PresentMember(actor.EmployeeId).Should().BeNull();
    }

    [Fact]
    public void Leave_NotJoined_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();

        // Act
        var result = session.Leave(PokerSessionActors.Voter("user-1"), Now);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Join_AfterLeaving_ShouldClearLeftOnAndKeepJoinTime()
    {
        // Arrange
        var session = CreateActiveSession();
        var actor = PokerSessionActors.Voter("user-1");
        session.Join(PokerSessionMemberMode.Participant, actor, Now);
        session.Leave(actor, Now.Plus(Duration.FromMinutes(1)));

        // Act
        var result = session.Join(PokerSessionMemberMode.Observer, actor, Now.Plus(Duration.FromMinutes(2)));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var member = session.Members.Should().ContainSingle().Subject;
        member.LeftOn.Should().BeNull();
        member.JoinedOn.Should().Be(Now);
        member.Mode.Should().Be(PokerSessionMemberMode.Observer);
    }

    [Fact]
    public void SubmitVote_NotJoined_ShouldJoinAsParticipant()
    {
        // Arrange
        var session = CreateActiveSession();
        var actor = PokerSessionActors.Voter("user-1");
        var round = session.AddRound("Story", null, PokerSessionActors.Facilitator).Value;

        // Act
        var result = session.SubmitVote(round.Id, "5", Scale.CurrentVersion, actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        session.PresentMember(actor.EmployeeId)!.Mode.Should().Be(PokerSessionMemberMode.Participant);
    }

    [Fact]
    public void SubmitVote_AfterLeaving_ShouldRejoinAsParticipant()
    {
        // Arrange
        var session = CreateActiveSession();
        var actor = PokerSessionActors.Voter("user-1");
        var round = session.AddRound("Story", null, PokerSessionActors.Facilitator).Value;
        session.Join(PokerSessionMemberMode.Observer, actor, Now);
        session.Leave(actor, Now);

        // Act
        var result = session.SubmitVote(round.Id, "5", Scale.CurrentVersion, actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        session.PresentMember(actor.EmployeeId)!.Mode.Should().Be(PokerSessionMemberMode.Participant);
    }

    [Fact]
    public void SubmitVote_AsObserver_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        var actor = PokerSessionActors.Voter("user-1");
        var round = session.AddRound("Story", null, PokerSessionActors.Facilitator).Value;
        session.Join(PokerSessionMemberMode.Observer, actor, Now);

        // Act
        var result = session.SubmitVote(round.Id, "5", Scale.CurrentVersion, actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        round.Votes.Should().BeEmpty();
    }

    [Fact]
    public void SubmitVote_PrivateSessionNotInvited_ShouldReadAsNotFound()
    {
        // Arrange
        var session = CreatePrivateSession();
        var round = session.AddRound("Story", null, PokerSessionActors.Facilitator).Value;

        // Act
        var result = session.SubmitVote(round.Id, "5", Scale.CurrentVersion, PokerSessionActors.Voter("someone"), Now);

        // Assert
        result.Error.Should().Be(PokerSession.NotFoundError);
        round.Votes.Should().BeEmpty();
        session.Members.Should().BeEmpty();
    }

    #endregion
}
