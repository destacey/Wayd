namespace Wayd.Common.Domain.Events.Planning.EstimationScales;

/// <summary>
/// Marks every event an estimation scale raises, so a module keeping a copy of the scales handles the marker
/// rather than a hand-listed set of types that drifts the day an event is added.
/// </summary>
/// <remarks>
/// <see cref="EstimationScaleBaselinedEvent"/> does not implement it: a baseline is recorded, never delivered.
/// </remarks>
public interface IEstimationScaleEvent : IEvent, IAggregateEvent
{
}
