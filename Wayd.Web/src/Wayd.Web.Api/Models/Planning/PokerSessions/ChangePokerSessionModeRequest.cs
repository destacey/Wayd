using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Web.Api.Models.Planning.PokerSessions;

public sealed class ChangePokerSessionModeRequest
{
    /// <summary>
    /// `Participant` votes and counts toward the voted and waiting totals; `Observer` watches without voting.
    /// </summary>
    public PokerSessionMemberMode Mode { get; set; }

    public ChangePokerSessionModeCommand ToChangePokerSessionModeCommand(Guid id) => new(id, Mode);
}

public sealed class ChangePokerSessionModeRequestValidator : CustomValidator<ChangePokerSessionModeRequest>
{
    public ChangePokerSessionModeRequestValidator()
    {
        RuleFor(r => r.Mode).IsInEnum();
    }
}
