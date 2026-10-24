using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An event about an intake request, declaring the read tier it belongs to.
/// </summary>
/// <remarks>
/// A request's history is filtered by tier rather than redacted field by field, so an event never mixes
/// tiers: a reader who holds only the summary is shown only summary events. Declared statically, like
/// <see cref="IDomainEventDescriptor.ActivityCategory"/>, so the tier is known from the event type alone.
/// </remarks>
public interface IIntakeRequestEvent : IDemandManagementEvent
{
    /// <summary>
    /// The tier a reader must hold to see this event.
    /// </summary>
    static abstract IntakeRequestReadTier ReadTier { get; }
}
