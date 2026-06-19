using Wayd.Planning.Application.EstimationScales.Commands;

namespace Wayd.Web.Api.Models.Planning.EstimationScales;

public class CreateEstimationScaleRequest
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public List<string> Values { get; set; } = [];

    public CreateEstimationScaleCommand ToCreateEstimationScaleCommand()
    {
        return new CreateEstimationScaleCommand(Name, Description, Values);
    }
}

public sealed class CreateEstimationScaleRequestValidator : CustomValidator<CreateEstimationScaleRequest>
{
    public CreateEstimationScaleRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
        RuleFor(r => r.Description).MaximumLength(1024);
        RuleFor(r => r.Values).NotEmpty().Must(v => v.Count >= 2).WithMessage("At least 2 values are required.");
        RuleForEach(r => r.Values).NotEmpty().MaximumLength(32);
    }
}
