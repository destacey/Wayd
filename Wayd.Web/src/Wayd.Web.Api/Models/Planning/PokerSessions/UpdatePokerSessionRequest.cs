using Wayd.Planning.Application.PokerSessions.Commands;

namespace Wayd.Web.Api.Models.Planning.PokerSessions;

public class UpdatePokerSessionRequest
{
    public string Name { get; set; } = default!;

    public int EstimationScaleId { get; set; }

    public UpdatePokerSessionCommand ToUpdatePokerSessionCommand(Guid id)
    {
        return new UpdatePokerSessionCommand(id, Name, EstimationScaleId);
    }
}

public sealed class UpdatePokerSessionRequestValidator : CustomValidator<UpdatePokerSessionRequest>
{
    public UpdatePokerSessionRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Name)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(r => r.EstimationScaleId)
            .GreaterThan(0);
    }
}
