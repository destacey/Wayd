using Wayd.Planning.Application.PokerSessions.Commands;

namespace Wayd.Web.Api.Models.Planning.PokerSessions;

public class FocusPokerRoundRequest
{
    /// <summary>
    /// The round to show everyone following the facilitator, or null to return them to the round in play.
    /// </summary>
    public Guid? RoundId { get; set; }

    public FocusPokerRoundCommand ToFocusPokerRoundCommand(Guid sessionId)
    {
        return new FocusPokerRoundCommand(sessionId, RoundId);
    }
}
