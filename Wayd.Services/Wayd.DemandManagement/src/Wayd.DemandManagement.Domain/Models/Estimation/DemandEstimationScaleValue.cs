namespace Wayd.DemandManagement.Domain.Models.Estimation;

/// <summary>
/// One value of a <see cref="DemandEstimationScaleVersion"/>, as Planning published it.
/// </summary>
/// <param name="Value">The text of the value, such as <c>M</c>, <c>8</c> or <c>?</c>.</param>
/// <param name="IsEstimate">
/// Whether the value is a size. False for a card such as <c>?</c>, which a request is never estimated with.
/// </param>
public sealed record DemandEstimationScaleValue(string Value, bool IsEstimate);
