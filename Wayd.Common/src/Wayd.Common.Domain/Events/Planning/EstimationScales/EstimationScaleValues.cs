namespace Wayd.Common.Domain.Events.Planning.EstimationScales;

/// <summary>
/// An estimation scale's editable details taken together, as
/// <see cref="EstimationScaleDetailsUpdatedEvent.Previous"/> records the values an edit replaced.
/// </summary>
public sealed record EstimationScaleDetails(string Name, string? Description);

/// <summary>
/// One published version of an estimation scale. A version never changes once published, so a consumer that
/// holds it by <paramref name="VersionId"/> holds it for good.
/// </summary>
/// <param name="Number">The version's number within its scale, starting at 1.</param>
/// <param name="Values">The values a vote may take, in display order.</param>
public sealed record EstimationScaleVersionValues(Guid VersionId, int Number, EstimationScaleValueValues[] Values);

/// <summary>
/// One value of an <see cref="EstimationScaleVersionValues"/>.
/// </summary>
/// <param name="IsEstimate">
/// False for a card such as <c>?</c>: a value that can be voted but never becomes a size.
/// </param>
public sealed record EstimationScaleValueValues(string Value, bool IsEstimate);
