using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Web.Api.Models.Planning.PokerSessions;

public class AddPokerRoundRequest
{
    public string? Label { get; set; }

    /// <summary>
    /// Optional detail about what is being estimated, such as acceptance criteria or notes for the team; at most 2048 characters.
    /// </summary>
    public string? Description { get; set; }

    public AddPokerRoundCommand ToAddPokerRoundCommand(Guid sessionId)
    {
        return new AddPokerRoundCommand(sessionId, Label, Description);
    }
}

public sealed class AddPokerRoundRequestValidator : CustomValidator<AddPokerRoundRequest>
{
    public AddPokerRoundRequestValidator()
    {
        RuleFor(r => r.Label).MaximumLength(512);
        RuleFor(r => r.Description).MaximumLength(PokerRound.DescriptionMaxLength);
    }
}
