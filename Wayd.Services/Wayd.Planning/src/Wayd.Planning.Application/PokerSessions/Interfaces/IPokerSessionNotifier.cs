using Wayd.Planning.Application.PokerSessions.Dtos;

namespace Wayd.Planning.Application.PokerSessions.Interfaces;

public interface IPokerSessionNotifier
{
    Task NotifySessionUpdated(Guid sessionId);
    Task NotifySessionCompleted(Guid sessionId);
    Task NotifyRoundAdded(Guid sessionId, PokerRoundDto round);
    Task NotifyRoundRemoved(Guid sessionId, Guid roundId);
    Task NotifyVoteSubmitted(Guid sessionId, Guid roundId, string participantId, string participantName);
    Task NotifyVoteWithdrawn(Guid sessionId, Guid roundId, string participantId);
    Task NotifyVotesRevealed(Guid sessionId, Guid roundId, IEnumerable<PokerVoteDto> votes);
    Task NotifyConsensusSet(Guid sessionId, Guid roundId, string estimate);
    Task NotifyRoundReset(Guid sessionId, Guid roundId);
    Task NotifyRoundLabelUpdated(Guid sessionId, Guid roundId, string? label);
    /// <summary>
    /// Tells the session's participants a round's description changed. The text is not sent, since it can
    /// run to 2048 characters; clients refetch the session.
    /// </summary>
    Task NotifyRoundDescriptionUpdated(Guid sessionId, Guid roundId);
}
