namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// Marks every business event raised by the Project Portfolio Management module.
/// </summary>
/// <remarks>
/// This interface exists so a projection or a routing decision can handle the marker rather than a
/// hand-listed set of types. A hand-listed set drifts silently the day someone adds an event and forgets
/// to register it: nothing fails, the copy just goes quietly stale. Handling the marker makes a new event
/// caught by default rather than by memory.
/// <para>
/// The module is moving toward business events named for what happened rather than a generic
/// <c>Updated</c>. <see cref="ProjectDetailsUpdatedEvent"/> predates that and still covers several fields
/// at once; new events should name their change.
/// </para>
/// </remarks>
public interface IPpmEvent : IEvent, IAggregateEvent
{
}
