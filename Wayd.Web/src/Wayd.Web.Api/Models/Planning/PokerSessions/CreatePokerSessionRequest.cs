using Wayd.Planning.Application.PokerSessions.Commands;

namespace Wayd.Web.Api.Models.Planning.PokerSessions;

public class CreatePokerSessionRequest
{
    public string Name { get; set; } = default!;
    public Guid EstimationScaleId { get; set; }

    public CreatePokerSessionCommand ToCreatePokerSessionCommand()
    {
        return new CreatePokerSessionCommand(Name, EstimationScaleId);
    }
}

public sealed class CreatePokerSessionRequestValidator : CustomValidator<CreatePokerSessionRequest>
{
    public CreatePokerSessionRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Name).NotEmpty().MaximumLength(256);
        RuleFor(r => r.EstimationScaleId).NotEmpty();
    }
}
