using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Web.Api.Models.Planning.PokerSessions;

public sealed class JoinPokerSessionRequest
{
    /// <summary>
    /// `Participant` (the default) votes and counts toward the voted and waiting totals; `Observer` watches
    /// without voting.
    /// </summary>
    public PokerSessionMemberMode Mode { get; set; } = PokerSessionMemberMode.Participant;

    public JoinPokerSessionCommand ToJoinPokerSessionCommand(Guid id) => new(id, Mode);
}

public sealed class JoinPokerSessionRequestValidator : CustomValidator<JoinPokerSessionRequest>
{
    public JoinPokerSessionRequestValidator()
    {
        RuleFor(r => r.Mode).IsInEnum();
    }
}
