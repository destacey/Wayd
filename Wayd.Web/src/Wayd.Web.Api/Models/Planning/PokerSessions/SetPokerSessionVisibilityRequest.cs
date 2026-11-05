using Wayd.Common.Domain.Enums;
using Wayd.Planning.Application.PokerSessions.Commands;

namespace Wayd.Web.Api.Models.Planning.PokerSessions;

public sealed class SetPokerSessionVisibilityRequest
{
    /// <summary>
    /// `Public`: anyone with the poker View permission can see and join the session. `Private`: only its
    /// facilitators, invited employees and the members of invited teams.
    /// </summary>
    public Visibility Visibility { get; set; }

    public SetPokerSessionVisibilityCommand ToSetPokerSessionVisibilityCommand(Guid id) => new(id, Visibility);
}

public sealed class SetPokerSessionVisibilityRequestValidator : CustomValidator<SetPokerSessionVisibilityRequest>
{
    public SetPokerSessionVisibilityRequestValidator()
    {
        RuleFor(r => r.Visibility).IsInEnum();
    }
}
