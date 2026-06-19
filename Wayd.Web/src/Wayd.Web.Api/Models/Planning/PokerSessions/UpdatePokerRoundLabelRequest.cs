using Wayd.Planning.Application.PokerSessions.Commands;

namespace Wayd.Web.Api.Models.Planning.PokerSessions;

public class UpdatePokerRoundLabelRequest
{
    public string? Label { get; set; }

    public UpdatePokerRoundLabelCommand ToUpdatePokerRoundLabelCommand(Guid sessionId, Guid roundId)
    {
        return new UpdatePokerRoundLabelCommand(sessionId, roundId, Label);
    }
}

public sealed class UpdatePokerRoundLabelRequestValidator : CustomValidator<UpdatePokerRoundLabelRequest>
{
    public UpdatePokerRoundLabelRequestValidator()
    {
        RuleFor(r => r.Label).MaximumLength(512);
    }
}
