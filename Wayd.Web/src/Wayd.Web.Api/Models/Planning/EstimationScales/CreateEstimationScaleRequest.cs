using Wayd.Planning.Application.EstimationScales.Commands;

namespace Wayd.Web.Api.Models.Planning.EstimationScales;

/// <summary>
/// A new estimation scale, created active with its values as version 1.
/// </summary>
public class CreateEstimationScaleRequest
{
    /// <summary>
    /// The scale's name, at most 128 characters.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// What the scale is for, at most 1024 characters.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The cards in the order they are shown, at least 2 of them estimates.
    /// </summary>
    public List<EstimationScaleValueRequest> Values { get; set; } = [];

    /// <summary>
    /// The command this request describes.
    /// </summary>
    public CreateEstimationScaleCommand ToCreateEstimationScaleCommand()
    {
        return new CreateEstimationScaleCommand(Name, Description, [.. Values.Select(v => v.ToInput())]);
    }
}

public sealed class CreateEstimationScaleRequestValidator : CustomValidator<CreateEstimationScaleRequest>
{
    public CreateEstimationScaleRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
        RuleFor(r => r.Description).MaximumLength(1024);
        RuleFor(r => r.Values).NotEmpty();
        RuleForEach(r => r.Values).ChildRules(v => v.RuleFor(x => x.Value).NotEmpty().MaximumLength(32));
    }
}
