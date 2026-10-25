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
}
