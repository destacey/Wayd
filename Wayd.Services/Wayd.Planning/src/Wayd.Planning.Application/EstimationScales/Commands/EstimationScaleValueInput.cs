using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.EstimationScales.Commands;

/// <summary>
/// One value of an estimation scale as a command supplies it.
/// </summary>
/// <param name="IsEstimate">False for a card such as <c>?</c>, which can be voted but never becomes an estimate.</param>
public sealed record EstimationScaleValueInput(string Value, bool IsEstimate)
{
    /// <summary>
    /// The domain value this input describes.
    /// </summary>
    public EstimationScaleValue ToValue() => new(Value, IsEstimate);
}

/// <summary>
/// The rules every set of values a command publishes must meet, checked before the domain sees them.
/// </summary>
public sealed class EstimationScaleValuesValidator : AbstractValidator<List<EstimationScaleValueInput>>
{
    public EstimationScaleValuesValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(v => v)
            .Must(v => v.Count(x => x.IsEstimate) >= EstimationScale.MinimumEstimateValues)
            .WithMessage($"An estimation scale must have at least {EstimationScale.MinimumEstimateValues} estimate values.")
            .Must(v => v.Select(x => x.Value?.Trim()).Distinct(StringComparer.Ordinal).Count() == v.Count)
            .WithMessage("Each value on an estimation scale must be unique.")
            .OverridePropertyName("Values");

        RuleForEach(v => v)
            .ChildRules(value => value.RuleFor(x => x.Value).NotEmpty().MaximumLength(EstimationScaleValue.MaxLength))
            .OverridePropertyName("Values");
    }
}
