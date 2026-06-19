using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.PlanningPoker;
using NodaTime.Extensions;

namespace Wayd.Planning.Domain.Tests.Sut.Models.PlanningPoker;

public class PokerSessionTests
{
    private static readonly Instant Now = DateTime.UtcNow.ToInstant();

    private static PokerSession CreateActiveSession()
    {
        return PokerSession.Create("Sprint 1 Refinement", 1, Guid.NewGuid().ToString(), Now).Value;
    }

    #region Create

    [Fact]
    public void Create_ValidParameters_ShouldReturnActiveSession()
    {
        // Arrange
        var scaleId = 1;
        var facilitatorId = Guid.NewGuid().ToString();

        // Act
        var result = PokerSession.Create("Sprint 1 Refinement", scaleId, facilitatorId, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Sprint 1 Refinement");
        result.Value.EstimationScaleId.Should().Be(scaleId);
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
        var result = PokerSession.Create("  ", 1, Guid.NewGuid().ToString(), Now);

        // Assert
        result.IsFailure.Should().BeTrue();
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
        var result = session.SubmitVote(round.Id, participantId, "5", Now);

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
        session.SubmitVote(round.Id, participantId, "3", Now);

        // Act
        var laterTimestamp = Now.Plus(Duration.FromMinutes(1));
        var result = session.SubmitVote(round.Id, participantId, "8", laterTimestamp);

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
        session.SubmitVote(round.Id, Guid.NewGuid().ToString(), "5", Now);
        session.RevealRound(round.Id);

        // Act
        var result = session.SubmitVote(round.Id, Guid.NewGuid().ToString(), "5", Now);

        // Assert
        result.IsFailure.Should().BeTrue();
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
        session.SubmitVote(round.Id, participantId, "5", Now);

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
        session.SubmitVote(round.Id, participantId, "5", Now);
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
        session.SubmitVote(round.Id, participantId, "5", Now);
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
        session.SubmitVote(round.Id, Guid.NewGuid().ToString(), "5", Now);

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
        session.SubmitVote(round.Id, Guid.NewGuid().ToString(), "5", Now);
        session.RevealRound(round.Id);
        session.SetConsensus(round.Id, "5");

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
        session.SubmitVote(round.Id, Guid.NewGuid().ToString(), "5", Now);
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
        session.SubmitVote(round.Id, Guid.NewGuid().ToString(), "5", Now);
        session.RevealRound(round.Id);
        session.SetConsensus(round.Id, "5");

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
        session.SubmitVote(round.Id, Guid.NewGuid().ToString(), "5", Now);
        session.RevealRound(round.Id);

        // Act
        var result = session.SetConsensus(round.Id, "5");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ConsensusEstimate.Should().Be("5");
        result.Value.Status.Should().Be(PokerRoundStatus.Accepted);
    }

    [Fact]
    public void SetConsensus_VotingRound_ShouldReturnFailure()
    {
        // Arrange
        var session = CreateActiveSession();
        var round = session.AddRound("WI-1: Test").Value;

        // Act
        var result = session.SetConsensus(round.Id, "5");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("revealed");
    }

    #endregion
}
