using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Web.Api.Models.Planning.PokerSessions;

/// <summary>
/// The new description for a poker round.
/// </summary>
public class UpdatePokerRoundDescriptionRequest
{
    /// <summary>
    /// Detail about what is being estimated, such as acceptance criteria or notes for the team; at most 2048 characters. Null or blank clears it.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Builds the command that applies this request to the given round.
    /// </summary>
    public UpdatePokerRoundDescriptionCommand ToUpdatePokerRoundDescriptionCommand(Guid sessionId, Guid roundId)
    {
        return new UpdatePokerRoundDescriptionCommand(sessionId, roundId, Description);
    }
}

public sealed class UpdatePokerRoundDescriptionRequestValidator : CustomValidator<UpdatePokerRoundDescriptionRequest>
{
    public UpdatePokerRoundDescriptionRequestValidator()
    {
        RuleFor(r => r.Description).MaximumLength(PokerRound.DescriptionMaxLength);
    }
}
