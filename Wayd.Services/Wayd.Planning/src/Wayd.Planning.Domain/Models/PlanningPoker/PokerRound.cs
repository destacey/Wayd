using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Planning.Domain.Enums;
using NodaTime;

namespace Wayd.Planning.Domain.Models.PlanningPoker;

public sealed class PokerRound : BaseEntity
{
    /// <summary>
    /// The longest description a round can hold.
    /// </summary>
    public const int DescriptionMaxLength = 2048;

    private readonly List<PokerVote> _votes = [];

    private PokerRound() { }

    internal PokerRound(Guid pokerSessionId, string? label, string? description, int order, PokerRoundSubject? subject = null)
    {
        PokerSessionId = pokerSessionId;
        Subject = subject;
        Label = label?.Trim();
        Description = NormalizeDescription(description);
        Order = order;
        Status = PokerRoundStatus.Voting;
    }

    public Guid PokerSessionId { get; private set; }

    /// <summary>
    /// Free-text label describing what is being estimated (e.g., a work item key + title, or any descriptive text).
    /// </summary>
    public string? Label { get; private set; }

    /// <summary>
    /// Optional free-text detail about what is being estimated, such as acceptance criteria or notes the team
    /// should read before voting. Blank text is stored as no description.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// The record the round estimates, or null for a round about whatever its <see cref="Label"/> describes. Set
    /// when the round is added and never changed.
    /// </summary>
    public PokerRoundSubject? Subject { get; private set; }

    public PokerRoundStatus Status { get; private set; }

    /// <summary>
    /// The consensus estimate agreed upon by the team after voting.
    /// </summary>
    public string? ConsensusEstimate { get; private set; }

    /// <summary>
    /// What came of last publishing the consensus to the round's <see cref="Subject"/>, or null when it has not
    /// been published since the round was last accepted. Cleared when the round is reset.
    /// </summary>
    public PokerRoundPublication? Publication { get; private set; }

    /// <summary>
    /// Whether the consensus can be published now: the round is about a record, its estimate is accepted, and it
    /// has not already been written to the record.
    /// </summary>
    public bool CanPublish => Subject is not null
        && Status == PokerRoundStatus.Accepted
        && Publication?.Outcome != PokerRoundPublicationOutcome.Published;

    /// <summary>
    /// The display order of this round within the session.
    /// </summary>
    public int Order { get; private set; }

    public IReadOnlyCollection<PokerVote> Votes => _votes.AsReadOnly();

    /// <summary>
    /// True for a round that holds nothing: no subject, no label, no description and no votes, such as the round
    /// opened automatically after an estimate is accepted. Requires <see cref="Votes"/> to be loaded.
    /// </summary>
    public bool IsEmpty => Subject is null && string.IsNullOrWhiteSpace(Label) && Description is null && _votes.Count == 0;

    /// <summary>
    /// Reveal all votes for this round.
    /// </summary>
    internal Result Reveal()
    {
        if (Status != PokerRoundStatus.Voting)
            return Result.Failure("Votes can only be revealed when the round is in Voting status.");

        Status = PokerRoundStatus.Revealed;
        return Result.Success();
    }

    /// <summary>
    /// Reset the round to allow re-voting. Clears all existing votes, and what came of publishing the consensus,
    /// so the round can be published again once it is accepted again. What an earlier publish wrote stays written.
    /// </summary>
    internal Result Reset()
    {
        if (Status is not (PokerRoundStatus.Voting or PokerRoundStatus.Revealed or PokerRoundStatus.Accepted))
            return Result.Failure("Round can only be reset from Voting, Revealed, or Accepted status.");

        _votes.Clear();
        ConsensusEstimate = null;
        Publication = null;
        Status = PokerRoundStatus.Voting;
        return Result.Success();
    }

    /// <summary>
    /// Set the consensus estimate for this round. A newly accepted estimate has never been published, whatever
    /// the round held before.
    /// </summary>
    internal Result SetConsensus(string estimate)
    {
        if (Status != PokerRoundStatus.Revealed)
            return Result.Failure("Consensus can only be set after votes are revealed.");

        ConsensusEstimate = Guard.Against.NullOrWhiteSpace(estimate, nameof(estimate)).Trim();
        // A publish that was under way when another facilitator reset the round saves its outcome after the
        // reset cleared it, which would otherwise stop this estimate ever being published.
        Publication = null;
        Status = PokerRoundStatus.Accepted;
        return Result.Success();
    }

    /// <summary>
    /// Records what came of publishing the consensus, replacing an earlier refusal.
    /// </summary>
    internal void RecordPublication(PokerRoundPublication publication)
    {
        if (!CanPublish)
            throw new InvalidOperationException("Only an accepted round about a record, not yet published, can record a publication.");

        Publication = Guard.Against.Null(publication, nameof(publication));
    }

    /// <summary>
    /// Update the label for this round.
    /// </summary>
    internal Result UpdateLabel(string? newLabel)
    {
        Label = newLabel?.Trim();
        return Result.Success();
    }

    /// <summary>
    /// Update the description for this round; a null or blank description clears it.
    /// </summary>
    internal Result UpdateDescription(string? newDescription)
    {
        Description = NormalizeDescription(newDescription);
        return Result.Success();
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    /// <summary>
    /// Remove a participant's vote from this round.
    /// </summary>
    internal Result RemoveVote(string participantId)
    {
        if (Status != PokerRoundStatus.Voting)
            return Result.Failure("Votes can only be withdrawn when the round is in Voting status.");

        var vote = _votes.FirstOrDefault(v => v.ParticipantId == participantId);
        if (vote is null)
            return Result.Failure("No vote found for this participant.");

        _votes.Remove(vote);
        return Result.Success();
    }

    /// <summary>
    /// Removes a participant's vote while the round is still Voting, for someone who stops taking part; a
    /// revealed or accepted round keeps it. Returns whether a vote was removed.
    /// </summary>
    internal bool WithdrawUnrevealedVote(string participantId)
    {
        if (Status != PokerRoundStatus.Voting)
            return false;

        return _votes.RemoveAll(v => v.ParticipantId == participantId) > 0;
    }

    /// <summary>
    /// Submit or update a vote for this round.
    /// </summary>
    internal Result AddOrUpdateVote(string participantId, string value, Instant timestamp)
    {
        if (Status != PokerRoundStatus.Voting)
            return Result.Failure("Votes can only be submitted when the round is in Voting status.");

        var existingVote = _votes.FirstOrDefault(v => v.ParticipantId == participantId);
        if (existingVote is not null)
        {
            existingVote.Update(value, timestamp);
        }
        else
        {
            _votes.Add(new PokerVote(Id, participantId, value, timestamp));
        }

        return Result.Success();
    }
}
