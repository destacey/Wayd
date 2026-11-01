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

    internal PokerRound(Guid pokerSessionId, string? label, string? description, int order)
    {
        PokerSessionId = pokerSessionId;
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

    public PokerRoundStatus Status { get; private set; }

    /// <summary>
    /// The consensus estimate agreed upon by the team after voting.
    /// </summary>
    public string? ConsensusEstimate { get; private set; }

    /// <summary>
    /// The display order of this round within the session.
    /// </summary>
    public int Order { get; private set; }

    public IReadOnlyCollection<PokerVote> Votes => _votes.AsReadOnly();

    /// <summary>
    /// True for a round that holds nothing: no label, no description and no votes, such as the round opened
    /// automatically after an estimate is accepted. Requires <see cref="Votes"/> to be loaded.
    /// </summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Label) && Description is null && _votes.Count == 0;

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
    /// Reset the round to allow re-voting. Clears all existing votes.
    /// </summary>
    internal Result Reset()
    {
        if (Status is not (PokerRoundStatus.Voting or PokerRoundStatus.Revealed or PokerRoundStatus.Accepted))
            return Result.Failure("Round can only be reset from Voting, Revealed, or Accepted status.");

        _votes.Clear();
        ConsensusEstimate = null;
        Status = PokerRoundStatus.Voting;
        return Result.Success();
    }

    /// <summary>
    /// Set the consensus estimate for this round.
    /// </summary>
    internal Result SetConsensus(string estimate)
    {
        if (Status != PokerRoundStatus.Revealed)
            return Result.Failure("Consensus can only be set after votes are revealed.");

        ConsensusEstimate = Guard.Against.NullOrWhiteSpace(estimate, nameof(estimate)).Trim();
        Status = PokerRoundStatus.Accepted;
        return Result.Success();
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
