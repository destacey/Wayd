using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Application.Notifications;

/// <summary>
/// Writes notifications and marks them read, treating an event that was already written as an outcome rather
/// than an error.
/// </summary>
/// <remarks>
/// A seam for the same reason as <c>IFollowStore</c>: two deliveries of one event racing each other both pass
/// an existence check, and only the unique key on (event, recipient) can say which lost. Recognising that
/// violation is provider-specific, so it lives in Infrastructure. Marking read is set-based for the same
/// reason "mark all read" must not load every unread row.
/// </remarks>
public interface INotificationStore
{
    /// <summary>
    /// Saves the notifications of one event, skipping recipients who already have one for it.
    /// </summary>
    /// <param name="notifications">Notifications that all share one <c>EventId</c>.</param>
    /// <returns>How many were added.</returns>
    Task<int> Add(IReadOnlyCollection<Notification> notifications, CancellationToken cancellationToken);

    /// <summary>Marks one of the person's notifications read, if it is theirs and unread.</summary>
    /// <returns>Whether the notification is theirs; true also when it was already read.</returns>
    Task<bool> MarkRead(Guid recipientEmployeeId, long notificationId, Instant readOn, CancellationToken cancellationToken);

    /// <summary>Marks every unread notification the person has about one record read.</summary>
    /// <returns>How many were marked.</returns>
    Task<int> MarkRecordRead(Guid recipientEmployeeId, string ownerType, Guid ownerId, Instant readOn, CancellationToken cancellationToken);

    /// <summary>Marks every unread notification the person has read.</summary>
    /// <returns>How many were marked.</returns>
    Task<int> MarkAllRead(Guid recipientEmployeeId, Instant readOn, CancellationToken cancellationToken);
}
