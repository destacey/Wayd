using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Application.Notifications;

/// <summary>
/// Tells the people who should know about a change to a record: its followers, and the participants the
/// record's own module names.
/// </summary>
/// <remarks>
/// Called only from the event handlers of the module that owns the record, never from an aggregate or a
/// command: notifications are a consumer of domain events, not a write path a module has to remember.
/// </remarks>
public interface INotificationWriter
{
    /// <summary>
    /// Whether a change recorded by <paramref name="source"/> can notify anyone at all: a person made it,
    /// directly or by setting off an automation, and notifications are switched on.
    /// </summary>
    /// <remarks>
    /// Cheap and independent of the record, so a handler asks it before reading anything — an import raises
    /// these events for every row and must not pay for a participant lookup that would be thrown away.
    /// </remarks>
    Task<bool> ShouldNotify(DomainEvent source, CancellationToken cancellationToken);

    /// <summary>
    /// Writes one notification per person who should know about <paramref name="change"/>, each carrying
    /// every reason that applies to them.
    /// </summary>
    /// <param name="change">What happened, to which record, and the event that recorded it.</param>
    /// <param name="participantEmployeeIds">
    /// The employees taking part in the record as its module defines it, read as the record is now.
    /// </param>
    /// <returns>How many notifications were added; zero when the event was already written.</returns>
    Task<int> Notify(RecordChange change, IReadOnlyCollection<Guid> participantEmployeeIds, CancellationToken cancellationToken);
}

/// <summary>A change to one record that people may need to hear about.</summary>
/// <param name="OwnerType">The record's registered owner type.</param>
/// <param name="OwnerId">The record's id.</param>
/// <param name="Event">What happened, as a notification list words it.</param>
/// <param name="Source">The domain event that recorded the change; supplies its id, actor and instant.</param>
public sealed record RecordChange(string OwnerType, Guid OwnerId, NotificationEvent Event, DomainEvent Source);
