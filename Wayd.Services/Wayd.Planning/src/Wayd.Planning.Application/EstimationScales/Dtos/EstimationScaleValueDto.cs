using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.EstimationScales.Dtos;

/// <summary>
/// One value on an estimation scale version.
/// </summary>
public sealed class EstimationScaleValueDto : IMapFrom<EstimationScaleValue>
{
    /// <summary>The text shown on the card and recorded as the vote.</summary>
    public required string Value { get; set; }

    /// <summary>False for a card such as <c>?</c>, which can be voted but never becomes an estimate.</summary>
    public bool IsEstimate { get; set; }
}
