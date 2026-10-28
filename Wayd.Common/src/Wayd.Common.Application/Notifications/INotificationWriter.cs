using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Application.Notifications;

/// <summary>
/// Tells the people who should know about a change to a record: its followers, and the people the record's
/// own module names as involved in it, each with the reason they are involved.
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
    /// <param name="involved">
    /// The people involved in the record as its module defines it, read as the record is now, each with why.
    /// One person may appear more than once, for different reasons. Followers are found here, not passed.
    /// </param>
    /// <returns>How many notifications were added; zero when the event was already written.</returns>
    /// <exception cref="ArgumentException">
    /// A reason that is not in <see cref="NotificationReasons.Produced"/>: a producer of a new reason adds it
    /// there first, so it can be muted from the day it is sent.
    /// </exception>
    Task<int> Notify(RecordChange change, IReadOnlyCollection<NotificationRecipient> involved, CancellationToken cancellationToken);
}

/// <summary>Someone a module names as involved in a record, and why.</summary>
/// <param name="EmployeeId">The employee involved.</param>
/// <param name="Reason">How they are involved: one reason, or several that all apply.</param>
public readonly record struct NotificationRecipient(Guid EmployeeId, NotificationReason Reason)
{
    /// <summary>Names every one of <paramref name="employeeIds"/> as involved for the same reason.</summary>
    public static IEnumerable<NotificationRecipient> For(NotificationReason reason, IEnumerable<Guid> employeeIds) =>
        employeeIds.Select(id => new NotificationRecipient(id, reason));
}

/// <summary>A change to one record that people may need to hear about.</summary>
/// <param name="OwnerType">The record's registered owner type.</param>
/// <param name="OwnerId">The record's id.</param>
/// <param name="Event">What happened, as a notification list words it.</param>
/// <param name="Source">The domain event that recorded the change; supplies its id, actor and instant.</param>
public sealed record RecordChange(string OwnerType, Guid OwnerId, NotificationEvent Event, DomainEvent Source);
