using Wayd.Common.Application.Dtos;

namespace Wayd.Planning.Application.PokerSessions.Dtos;

/// <summary>
/// A poker round that estimated a record, with the session it belongs to, as the record's own page lists it.
/// </summary>
public sealed record PokerSubjectRoundDto
{
    /// <summary>The session the round belongs to.</summary>
    public required NavigationDto Session { get; set; }

    /// <summary>The round.</summary>
    public Guid RoundId { get; set; }

    /// <summary>The round's status: Voting, Revealed or Accepted.</summary>
    public required string Status { get; set; }

    /// <summary>The consensus the team settled on, a card such as <c>?</c> included, or null until one is accepted.</summary>
    public string? ConsensusEstimate { get; set; }

    /// <summary>
    /// What came of last publishing the consensus to the record, or null when it has not been published since
    /// the round was last accepted.
    /// </summary>
    public PokerRoundPublicationDto? Publication { get; set; }
}
