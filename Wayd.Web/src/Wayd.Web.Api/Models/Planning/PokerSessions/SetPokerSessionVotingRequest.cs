using Wayd.Planning.Application.PokerSessions.Commands;

namespace Wayd.Web.Api.Models.Planning.PokerSessions;

/// <summary>
/// Turns the caller's voting on or off in a poker session.
/// </summary>
public sealed class SetPokerSessionVotingRequest
{
    /// <summary>
    /// True to vote, counting toward the voted and waiting totals; false to take part without voting.
    /// </summary>
    public bool IsVoting { get; set; }

    /// <summary>
    /// The command that applies this request to the session.
    /// </summary>
    public SetPokerSessionVotingCommand ToSetPokerSessionVotingCommand(Guid id) => new(id, IsVoting);
}
