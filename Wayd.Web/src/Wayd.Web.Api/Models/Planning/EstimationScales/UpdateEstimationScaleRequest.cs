using Wayd.Planning.Application.EstimationScales.Commands;

namespace Wayd.Web.Api.Models.Planning.EstimationScales;

/// <summary>
/// An estimation scale's name, description and values, all set from the body.
/// </summary>
public class UpdateEstimationScaleRequest
{
    /// <summary>
    /// The scale's id, matching the one in the path.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The scale's name, at most 128 characters.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// What the scale is for, at most 1024 characters. Omitting it clears it.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The cards in the order they are shown, at least 2 of them estimates. Values that differ from the current
    /// version's, in text, flag or order, publish the next version; the same values publish nothing.
    /// </summary>
    public List<EstimationScaleValueRequest> Values { get; set; } = [];

    /// <summary>
    /// The command this request describes.
    /// </summary>
    public UpdateEstimationScaleCommand ToUpdateEstimationScaleCommand()
    {
        return new UpdateEstimationScaleCommand(Id, Name, Description, [.. Values.Select(v => v.ToInput())]);
    }
}

public sealed class UpdateEstimationScaleRequestValidator : CustomValidator<UpdateEstimationScaleRequest>
{
    public UpdateEstimationScaleRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Id).NotEmpty();
        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
        RuleFor(r => r.Description).MaximumLength(1024);
        RuleFor(r => r.Values).NotEmpty();
        RuleForEach(r => r.Values).ChildRules(v => v.RuleFor(x => x.Value).NotEmpty().MaximumLength(32));
    }
}
