namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// Marks every business event raised by the Demand Management module.
/// </summary>
/// <remarks>
/// Lets a consumer handle the module's events as a family rather than a hand-listed set of types, which
/// drifts silently the day an event is added and nobody registers it.
/// </remarks>
public interface IDemandManagementEvent : IEvent, IAggregateEvent
{
}
