using Wayd.Planning.Application.PokerSessions.Commands;

namespace Wayd.Web.Api.Models.Planning.PokerSessions;

public class AddPokerRoundRequest
{
    public string? Label { get; set; }

    public AddPokerRoundCommand ToAddPokerRoundCommand(Guid sessionId)
    {
        return new AddPokerRoundCommand(sessionId, Label);
    }
}

public sealed class AddPokerRoundRequestValidator : CustomValidator<AddPokerRoundRequest>
{
    public AddPokerRoundRequestValidator()
    {
        RuleFor(r => r.Label).MaximumLength(512);
    }
}
