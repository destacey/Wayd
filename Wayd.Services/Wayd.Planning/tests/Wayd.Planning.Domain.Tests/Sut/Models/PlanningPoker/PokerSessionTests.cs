using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Planning;
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
    public void Complete_ShouldClearTheFocus()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1", null, PokerSessionActors.Facilitator).Value;
        session.FocusRound(round.Id, PokerSessionActors.Facilitator);

        // Act
        var result = session.Complete(PokerSessionActors.Facilitator, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.Rounds.Should().ContainSingle();
        session.FocusedRoundId.Should().BeNull();
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

    [Fact]
    public void RemoveRound_TheFocusedRound_ShouldClearTheFocus()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1", null, PokerSessionActors.Facilitator).Value;
        session.FocusRound(round.Id, PokerSessionActors.Facilitator);

        // Act
        var result = session.RemoveRound(round.Id, PokerSessionActors.Facilitator);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.FocusedRoundId.Should().BeNull();
    }

    [Fact]
    public void RemoveRound_AnotherRound_ShouldKeepTheFocus()
    {
        // Arrange
        var session = CreateActiveSession();
        var focused = session.AddRound("WI-1", null, PokerSessionActors.Facilitator).Value;
        var other = session.AddRound("WI-2", null, PokerSessionActors.Facilitator).Value;
        session.FocusRound(focused.Id, PokerSessionActors.Facilitator);

        // Act
        var result = session.RemoveRound(other.Id, PokerSessionActors.Facilitator);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.FocusedRoundId.Should().Be(focused.Id);
    }

    #endregion

    #region FocusRound

    [Fact]
    public void FocusRound_ARoundOfTheSession_ShouldPointTheSessionAtIt()
    {
        // Arrange
        var session = CreateActiveSession();
        session.AddRound("WI-1", null, PokerSessionActors.Facilitator);
        var round = session.AddRound("WI-2", null, PokerSessionActors.Facilitator).Value;

        // Act
        var result = session.FocusRound(round.Id, PokerSessionActors.Facilitator);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.FocusedRoundId.Should().Be(round.Id);
    }

    [Fact]
    public void FocusRound_AnotherRound_ShouldReplaceTheFocus()
    {
        // Arrange
        var session = CreateActiveSession();
        var first = session.AddRound("WI-1", null, PokerSessionActors.Facilitator).Value;
        var second = session.AddRound("WI-2", null, PokerSessionActors.Facilitator).Value;
        session.FocusRound(first.Id, PokerSessionActors.Facilitator);

        // Act
        var result = session.FocusRound(second.Id, PokerSessionActors.Administrator);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.FocusedRoundId.Should().Be(second.Id);
    }

    [Fact]
    public void FocusRound_Null_ShouldClearTheFocus()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1", null, PokerSessionActors.Facilitator).Value;
        session.FocusRound(round.Id, PokerSessionActors.Facilitator);

        // Act
        var result = session.FocusRound(null, PokerSessionActors.Facilitator);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.FocusedRoundId.Should().BeNull();
    }

    [Fact]
    public void FocusRound_UnknownRound_ShouldReturnFailureAndKeepTheFocus()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1", null, PokerSessionActors.Facilitator).Value;
        session.FocusRound(round.Id, PokerSessionActors.Facilitator);

        // Act
        var result = session.FocusRound(Guid.NewGuid(), PokerSessionActors.Facilitator);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Round not found.");
        session.FocusedRoundId.Should().Be(round.Id);
    }

    [Fact]
    public void FocusRound_ByNonFacilitator_ShouldReturnNotAFacilitator()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1", null, PokerSessionActors.Facilitator).Value;

        // Act
        var result = session.FocusRound(round.Id, PokerSessionActors.Voter("someone"));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PokerSession.NotAFacilitatorError);
        session.FocusedRoundId.Should().BeNull();
    }

    [Fact]
    public void FocusRound_ByOutsiderOnPrivateSession_ShouldReadAsNotFound()
    {
        // Arrange
        var session = CreatePrivateSession();
        var round = session.AddRound("WI-1", null, PokerSessionActors.Facilitator).Value;

        // Act
        var result = session.FocusRound(round.Id, PokerSessionActors.Voter("someone"));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PokerSession.NotFoundError);
        session.FocusedRoundId.Should().BeNull();
    }

    [Fact]
    public void FocusRound_CompletedSession_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1", null, PokerSessionActors.Facilitator).Value;
        session.Complete(PokerSessionActors.Facilitator, Now);

        // Act
        var result = session.FocusRound(round.Id, PokerSessionActors.Facilitator);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Cannot focus a round when the session is not active.");
        session.FocusedRoundId.Should().BeNull();
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

    [Fact]
    public void SetConsensus_OnTheFocusedRound_ShouldClearTheFocus()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1", null, PokerSessionActors.Facilitator).Value;
        session.FocusRound(round.Id, PokerSessionActors.Facilitator);
        session.SubmitVote(round.Id, "5", Scale.CurrentVersion, PokerSessionActors.Voter("user-1"), Now);
        session.RevealRound(round.Id, PokerSessionActors.Facilitator);

        // Act
        var result = session.SetConsensus(round.Id, "5", Scale.CurrentVersion, PokerSessionActors.Facilitator);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.FocusedRoundId.Should().BeNull();
    }

    [Fact]
    public void SetConsensus_OnAnotherRound_ShouldKeepTheFocus()
    {
        // Arrange
        var session = CreateActiveSession();
        var focused = session.AddRound("WI-1", null, PokerSessionActors.Facilitator).Value;
        var estimated = session.AddRound("WI-2", null, PokerSessionActors.Facilitator).Value;
        session.FocusRound(focused.Id, PokerSessionActors.Facilitator);
        session.SubmitVote(estimated.Id, "5", Scale.CurrentVersion, PokerSessionActors.Voter("user-1"), Now);
        session.RevealRound(estimated.Id, PokerSessionActors.Facilitator);

        // Act
        var result = session.SetConsensus(estimated.Id, "5", Scale.CurrentVersion, PokerSessionActors.Facilitator);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.FocusedRoundId.Should().Be(focused.Id);
    }

    [Fact]
    public void SetConsensus_Refused_ShouldKeepTheFocus()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1", null, PokerSessionActors.Facilitator).Value;
        session.FocusRound(round.Id, PokerSessionActors.Facilitator);

        // Act
        var result = session.SetConsensus(round.Id, "5", Scale.CurrentVersion, PokerSessionActors.Facilitator);

        // Assert
        result.IsFailure.Should().BeTrue();
        session.FocusedRoundId.Should().Be(round.Id);
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
        PokerSession[] sessions = [CreateActiveSession(), CreatePrivateSession(), new PokerSessionFaker().WithVisibility(Visibility.Private).Generate(), CreateHostedSession()];
        PokerSessionActor[] actors =
        [
            PokerSessionActors.Facilitator,
            PokerSessionActors.Administrator,
            PokerSessionActors.Employee(InvitedEmployeeId),
            PokerSessionActors.Employee(Guid.NewGuid(), InvitedTeamId),
            PokerSessionActors.Voter("someone"),
            PokerSessionActors.HostManager,
        ];

        foreach (var actor in actors)
        {
            // Act
            Guid[] managedHostIds = actor.ManagesHost ? [DeskHost.Id] : [];
            var filter = PokerSession.AccessibleTo(actor.EmployeeId, actor.IsAdministrator, [.. actor.TeamIds], managedHostIds).Compile();

            // Assert
            foreach (var session in sessions)
                filter(session).Should().Be(session.CanAccess(actor), "the query filter and the aggregate must agree for {0}", actor.UserId);
        }
    }

    [Fact]
    public void AccessibleTo_ReaderWithoutEmployee_ShouldSeeOnlyPublicSessions()
    {
        // Arrange
        var filter = PokerSession.AccessibleTo(null, false, [], []).Compile();

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
    public void Join_ShouldAddAVotingMember()
    {
        // Arrange
        var session = CreateActiveSession();
        var actor = PokerSessionActors.Voter("user-1");

        // Act
        var result = session.Join(true, actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var member = session.Members.Should().ContainSingle().Subject;
        member.EmployeeId.Should().Be(actor.EmployeeId);
        member.IsVoting.Should().BeTrue();
        member.JoinedOn.Should().Be(Now);
        result.Value.WithdrawnRoundIds.Should().BeEmpty();
    }

    [Fact]
    public void Join_PrivateSessionNotInvited_ShouldReadAsNotFound()
    {
        // Arrange
        var session = CreatePrivateSession();

        // Act
        var result = session.Join(true, PokerSessionActors.Voter("someone"), Now);

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
        var result = session.Join(false, PokerSessionActors.Employee(Guid.NewGuid(), InvitedTeamId), Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Member.IsVoting.Should().BeFalse();
    }

    [Fact]
    public void Join_CompletedSession_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        session.Complete(PokerSessionActors.Facilitator, Now);

        // Act
        var result = session.Join(true, PokerSessionActors.Voter("user-1"), Now);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SetVoting_Off_ShouldWithdrawUnrevealedVotesOnly()
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
        var result = session.SetVoting(false, actor);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.WithdrawnRoundIds.Should().Equal(voting.Id);
        voting.Votes.Should().BeEmpty();
        revealed.Votes.Should().ContainSingle();
        session.FindMember(actor.EmployeeId)!.IsVoting.Should().BeFalse();
    }

    [Fact]
    public void SetVoting_On_ShouldLetTheMemberVote()
    {
        // Arrange
        var session = CreateActiveSession();
        var actor = PokerSessionActors.Voter("user-1");
        var round = session.AddRound("Story", null, PokerSessionActors.Facilitator).Value;
        session.Join(false, actor, Now);

        // Act
        var result = session.SetVoting(true, actor);
        var vote = session.SubmitVote(round.Id, "5", Scale.CurrentVersion, actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.WithdrawnRoundIds.Should().BeEmpty();
        vote.IsSuccess.Should().BeTrue();
        vote.Value.Should().BeFalse();
    }

    [Fact]
    public void SetVoting_NotJoined_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();

        // Act
        var result = session.SetVoting(false, PokerSessionActors.Voter("user-1"));

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Join_Again_ShouldChangeNothing()
    {
        // Arrange
        var session = CreateActiveSession();
        var actor = PokerSessionActors.Voter("user-1");
        session.Join(true, actor, Now);
        session.SetVoting(false, actor);

        // Act
        var result = session.Join(true, actor, Now.Plus(Duration.FromMinutes(2)));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.WithdrawnRoundIds.Should().BeEmpty();
        var member = session.Members.Should().ContainSingle().Subject;
        member.JoinedOn.Should().Be(Now);
        member.IsVoting.Should().BeFalse();
    }

    [Fact]
    public void SubmitVote_NotJoined_ShouldJoinVoting()
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
        session.FindMember(actor.EmployeeId)!.IsVoting.Should().BeTrue();
    }

    [Fact]
    public void SubmitVote_NotVoting_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        var actor = PokerSessionActors.Voter("user-1");
        var round = session.AddRound("Story", null, PokerSessionActors.Facilitator).Value;
        session.Join(false, actor, Now);

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

    #region Hosted sessions

    private static readonly PokerSessionHost DeskHost = new(PokerSessionHostKind.IntakeDesk, Guid.Parse("7a1f0c00-0000-4000-a000-0000000d0001"));

    private static PokerSession CreateHostedSession() =>
        PokerSession.CreateHosted("Desk Refinement", Scale.CurrentVersion, DeskHost, PokerSessionActors.FacilitatorEmployeeId, Now).Value;

    private static PokerRoundSubject ARequest(string key = "42", string title = "Export invoices") =>
        new(PokerRoundSubjectKind.IntakeRequest, Guid.NewGuid(), key, title, "Finance needs a monthly export.");

    [Fact]
    public void CreateHosted_ShouldBePrivateAndHeldByItsHost()
    {
        // Act
        var result = PokerSession.CreateHosted("Desk Refinement", Scale.CurrentVersion, DeskHost, PokerSessionActors.FacilitatorEmployeeId, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Host.Should().Be(DeskHost);
        result.Value.Visibility.Should().Be(Visibility.Private);
        result.Value.Status.Should().Be(PokerSessionStatus.Active);
        result.Value.Facilitators.Should().ContainSingle().Which.EmployeeId.Should().Be(PokerSessionActors.FacilitatorEmployeeId);
    }

    [Fact]
    public void CreateHosted_OnAnEarlierVersionOfAnInactiveScale_ShouldVoteOnThatVersion()
    {
        // Arrange
        var scale = new EstimationScaleFaker().WithValues("S", "M", "L").Generate();
        var pinned = scale.CurrentVersion;
        scale.PublishVersion([new("S", true), new("M", true), new("L", true), new("XL", true)], EventActor.System, Now);
        scale.Deactivate(EventActor.System, Now);

        // Act
        var result = PokerSession.CreateHosted("Desk Refinement", pinned, DeskHost, PokerSessionActors.FacilitatorEmployeeId, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EstimationScaleVersionId.Should().Be(pinned.Id);
        result.Value.EstimationScaleVersionId.Should().NotBe(scale.CurrentVersion.Id);
    }

    [Fact]
    public void CanManage_HostManager_ShouldRunAHostedSessionOnly()
    {
        // Arrange
        var hosted = CreateHostedSession();
        var standalone = CreateActiveSession();

        // Act & Assert
        hosted.CanManage(PokerSessionActors.HostManager).Should().BeTrue();
        hosted.CanAccess(PokerSessionActors.HostManager).Should().BeTrue();
        standalone.CanManage(PokerSessionActors.HostManager).Should().BeFalse();
    }

    [Fact]
    public void CanAccess_HostedSession_ShouldRefuseSomeoneNotOnIt()
    {
        // Arrange
        var hosted = CreateHostedSession();

        // Act & Assert
        hosted.CanAccess(PokerSessionActors.Voter("someone")).Should().BeFalse();
    }

    [Fact]
    public void SetVisibility_HostedSessionToPublic_ShouldFail()
    {
        // Arrange
        var hosted = CreateHostedSession();

        // Act
        var result = hosted.SetVisibility(Visibility.Public, PokerSessionActors.HostManager);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PokerSession.HostedSessionPublicError);
        hosted.Visibility.Should().Be(Visibility.Private);
    }

    [Fact]
    public void AddSubjectRounds_ShouldAddARoundAboutEachRecordInOrder()
    {
        // Arrange
        var hosted = CreateHostedSession();
        hosted.AddRound("Warm-up", null, PokerSessionActors.Facilitator);
        var first = ARequest("41", "First");
        var second = ARequest("42", "Second");

        // Act
        var result = hosted.AddSubjectRounds([first, second], PokerSessionActors.HostManager);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Select(r => r.Subject).Should().Equal(first, second);
        result.Value.Select(r => r.Order).Should().Equal(1, 2);
        result.Value.Should().AllSatisfy(r =>
        {
            r.Label.Should().BeNull();
            r.Status.Should().Be(PokerRoundStatus.Voting);
        });
        hosted.Rounds.Should().HaveCount(3);
    }

    [Fact]
    public void AddSubjectRounds_RecordAlreadyInTheSession_ShouldFailAndAddNothing()
    {
        // Arrange
        var hosted = CreateHostedSession();
        var request = ARequest();
        hosted.AddSubjectRounds([request], PokerSessionActors.HostManager);

        // Act
        var result = hosted.AddSubjectRounds([ARequest("43"), request], PokerSessionActors.HostManager);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PokerSession.SubjectAlreadyInSessionError);
        hosted.Rounds.Should().ContainSingle();
    }

    [Fact]
    public void AddSubjectRounds_SameRecordTwice_ShouldFail()
    {
        // Arrange
        var hosted = CreateHostedSession();
        var request = ARequest();

        // Act
        var result = hosted.AddSubjectRounds([request, request], PokerSessionActors.HostManager);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PokerSession.SubjectAlreadyInSessionError);
        hosted.Rounds.Should().BeEmpty();
    }

    [Fact]
    public void AddSubjectRounds_BySomeoneWhoDoesNotRunTheSession_ShouldFail()
    {
        // Arrange
        var hosted = CreateHostedSession();

        // Act
        var result = hosted.AddSubjectRounds([ARequest()], PokerSessionActors.Voter("someone"));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PokerSession.NotFoundError);
        hosted.Rounds.Should().BeEmpty();
    }

    [Fact]
    public void AddSubjectRounds_CompletedSession_ShouldFail()
    {
        // Arrange
        var hosted = CreateHostedSession();
        hosted.Complete(PokerSessionActors.HostManager, Now);

        // Act
        var result = hosted.AddSubjectRounds([ARequest()], PokerSessionActors.HostManager);

        // Assert
        result.IsFailure.Should().BeTrue();
        hosted.Rounds.Should().BeEmpty();
    }

    [Fact]
    public void Complete_ShouldKeepAnUnvotedRoundAboutARecord()
    {
        // Arrange
        var hosted = CreateHostedSession();
        hosted.AddSubjectRounds([ARequest()], PokerSessionActors.HostManager);

        // Act
        var result = hosted.Complete(PokerSessionActors.HostManager, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        hosted.Rounds.Should().ContainSingle().Which.Subject.Should().NotBeNull();
    }

    #endregion

    #region Publishing

    /// <summary>A hosted session with one round about a request, accepted at <paramref name="consensus"/>.</summary>
    private static (PokerSession Session, PokerRound Round) AHostedSessionWithAnAcceptedRound(string consensus = "5")
    {
        var hosted = CreateHostedSession();
        var round = hosted.AddSubjectRounds([ARequest()], PokerSessionActors.HostManager).Value.Single();
        hosted.RevealRound(round.Id, PokerSessionActors.HostManager);
        hosted.SetConsensus(round.Id, consensus, Scale.CurrentVersion, PokerSessionActors.HostManager);
        return (hosted, round);
    }

    [Fact]
    public void RoundsToPublish_WithNoRoundNamed_ShouldReturnEveryAcceptedSubjectRoundNotYetPublished()
    {
        // Arrange — one accepted, one already published, one still voting, one free-text and accepted
        var (hosted, accepted) = AHostedSessionWithAnAcceptedRound();
        var added = hosted.AddSubjectRounds([ARequest("43"), ARequest("44")], PokerSessionActors.HostManager).Value;
        var published = added[0];
        hosted.RevealRound(published.Id, PokerSessionActors.HostManager);
        hosted.SetConsensus(published.Id, "8", Scale.CurrentVersion, PokerSessionActors.HostManager);
        hosted.RecordRoundPublication(published.Id, Result.Success(), PokerSessionActors.HostManager, Now);
        var freeText = hosted.AddRound("Spike", null, PokerSessionActors.HostManager).Value;
        hosted.RevealRound(freeText.Id, PokerSessionActors.HostManager);
        hosted.SetConsensus(freeText.Id, "3", Scale.CurrentVersion, PokerSessionActors.HostManager);

        // Act
        var result = hosted.RoundsToPublish(null, PokerSessionActors.HostManager);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Id.Should().Be(accepted.Id);
    }

    [Fact]
    public void RoundsToPublish_OnACompletedSession_ShouldStillReturnTheRound()
    {
        // Arrange
        var (hosted, round) = AHostedSessionWithAnAcceptedRound();
        hosted.Complete(PokerSessionActors.HostManager, Now);

        // Act
        var result = hosted.RoundsToPublish(round.Id, PokerSessionActors.HostManager);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Id.Should().Be(round.Id);
    }

    [Fact]
    public void RoundsToPublish_FreeTextRound_ShouldFail()
    {
        // Arrange
        var hosted = CreateHostedSession();
        var round = hosted.AddRound("Spike", null, PokerSessionActors.HostManager).Value;
        hosted.RevealRound(round.Id, PokerSessionActors.HostManager);
        hosted.SetConsensus(round.Id, "3", Scale.CurrentVersion, PokerSessionActors.HostManager);

        // Act
        var result = hosted.RoundsToPublish(round.Id, PokerSessionActors.HostManager);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PokerSession.RoundHasNoSubjectError);
    }

    [Fact]
    public void RoundsToPublish_RoundNotAccepted_ShouldFail()
    {
        // Arrange
        var hosted = CreateHostedSession();
        var round = hosted.AddSubjectRounds([ARequest()], PokerSessionActors.HostManager).Value.Single();
        hosted.RevealRound(round.Id, PokerSessionActors.HostManager);

        // Act
        var result = hosted.RoundsToPublish(round.Id, PokerSessionActors.HostManager);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PokerSession.RoundNotAcceptedError);
    }

    [Fact]
    public void RoundsToPublish_BySomeoneWhoDoesNotRunTheSession_ShouldReadAsNotFound()
    {
        // Arrange
        var (hosted, round) = AHostedSessionWithAnAcceptedRound();

        // Act
        var result = hosted.RoundsToPublish(round.Id, PokerSessionActors.Voter("someone"));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PokerSession.NotFoundError);
    }

    [Fact]
    public void RecordRoundPublication_Taken_ShouldMarkTheRoundPublishedAndStopItPublishingAgain()
    {
        // Arrange
        var (hosted, round) = AHostedSessionWithAnAcceptedRound();

        // Act
        hosted.RecordRoundPublication(round.Id, Result.Success(), PokerSessionActors.HostManager, Now);

        // Assert
        round.Publication!.Outcome.Should().Be(PokerRoundPublicationOutcome.Published);
        round.Publication.At.Should().Be(Now);
        round.Publication.ByEmployeeId.Should().Be(PokerSessionActors.HostManager.EmployeeId);
        round.Publication.RefusalReason.Should().BeNull();
        var again = hosted.RoundsToPublish(round.Id, PokerSessionActors.HostManager);
        again.IsFailure.Should().BeTrue();
        again.Error.Should().Be(PokerSession.RoundAlreadyPublishedError);
    }

    [Fact]
    public void RecordRoundPublication_Refused_ShouldKeepTheReasonAndLetTheRoundPublishAgain()
    {
        // Arrange
        var (hosted, round) = AHostedSessionWithAnAcceptedRound();

        // Act
        hosted.RecordRoundPublication(round.Id, Result.Failure("The request has moved to another desk."), PokerSessionActors.HostManager, Now);

        // Assert
        round.Publication!.Outcome.Should().Be(PokerRoundPublicationOutcome.Refused);
        round.Publication.RefusalReason.Should().Be("The request has moved to another desk.");
        hosted.RoundsToPublish(round.Id, PokerSessionActors.HostManager).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void SetConsensus_OnARoundStillMarkedPublished_ShouldClearThePublication()
    {
        // Arrange — a publish under way during a reset saves its outcome onto the round after the reset
        var (hosted, round) = AHostedSessionWithAnAcceptedRound();
        hosted.ResetRound(round.Id, PokerSessionActors.HostManager);
        hosted.RevealRound(round.Id, PokerSessionActors.HostManager);
        typeof(PokerRound).GetProperty(nameof(PokerRound.Publication))!
            .SetValue(round, PokerRoundPublication.Published(PokerSessionActors.HostManager.EmployeeId, Now));

        // Act
        hosted.SetConsensus(round.Id, "8", Scale.CurrentVersion, PokerSessionActors.HostManager);

        // Assert
        round.Publication.Should().BeNull();
        hosted.RoundsToPublish(round.Id, PokerSessionActors.HostManager).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ResetRound_PublishedRound_ShouldClearThePublicationSoItCanBePublishedOnceAcceptedAgain()
    {
        // Arrange
        var (hosted, round) = AHostedSessionWithAnAcceptedRound();
        hosted.RecordRoundPublication(round.Id, Result.Success(), PokerSessionActors.HostManager, Now);

        // Act
        hosted.ResetRound(round.Id, PokerSessionActors.HostManager);

        // Assert
        round.Publication.Should().BeNull();
        hosted.RevealRound(round.Id, PokerSessionActors.HostManager);
        hosted.SetConsensus(round.Id, "8", Scale.CurrentVersion, PokerSessionActors.HostManager);
        hosted.RoundsToPublish(round.Id, PokerSessionActors.HostManager).IsSuccess.Should().BeTrue();
    }

    #endregion
}
