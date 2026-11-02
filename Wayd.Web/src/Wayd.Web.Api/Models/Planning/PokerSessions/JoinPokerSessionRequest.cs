using Wayd.Planning.Application.PokerSessions.Commands;

namespace Wayd.Web.Api.Models.Planning.PokerSessions;

/// <summary>
/// Joins the caller to a poker session.
/// </summary>
public sealed class JoinPokerSessionRequest
{
    /// <summary>
    /// True (the default) to vote, counting toward the voted and waiting totals; false to take part without voting.
    /// Ignored when the caller has already joined.
    /// </summary>
    public bool IsVoting { get; set; } = true;

    /// <summary>
    /// The command that applies this request to the session.
    /// </summary>
    public JoinPokerSessionCommand ToJoinPokerSessionCommand(Guid id) => new(id, IsVoting);
}
