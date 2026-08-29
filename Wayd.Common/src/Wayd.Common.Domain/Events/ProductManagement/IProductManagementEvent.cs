namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// Marks every business event raised by the Product Management module.
/// </summary>
/// <remarks>
/// The module raises business events only — named for what happened, never a generic <c>Updated</c> —
/// and lets replication subscribe to the subset it needs, rather than raising a second coarse family
/// that would double-fire on every change.
/// <para>
/// This interface exists so a projection can handle the marker rather than a hand-listed set of types.
/// A hand-listed set drifts silently the day someone adds an event and forgets to register it: nothing
/// fails, the copy just goes quietly stale. Handling the marker makes a new event caught by default
/// rather than by memory.
/// </para>
/// </remarks>
public interface IProductManagementEvent : IEvent
{
}
