using Wayd.Planning.Application.EstimationScales.Commands;

namespace Wayd.Web.Api.Models.Planning.EstimationScales;

/// <summary>
/// One value on an estimation scale: a card participants can vote.
/// </summary>
public sealed class EstimationScaleValueRequest
{
    /// <summary>
    /// The text shown on the card and recorded as the vote, such as `5`, `M` or `?`. At most 32 characters, and
    /// unique on the scale.
    /// </summary>
    public string Value { get; set; } = default!;

    /// <summary>
    /// Whether the value is a size. False for a card such as `?`, `∞` or `☕`, which can be voted but never
    /// becomes an estimate. A scale needs at least 2 estimate values.
    /// </summary>
    public bool IsEstimate { get; set; }

    /// <summary>
    /// The command input this request describes.
    /// </summary>
    public EstimationScaleValueInput ToInput() => new(Value, IsEstimate);
}
