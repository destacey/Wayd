using Wayd.Common.Application.Notifications;
using NodaTime;
using Wayd.Common.Domain.Notifications;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.Common.Application.Tests.Infrastructure;

/// <summary>
/// An <see cref="INotificationStore"/> over the fake context's notifications, with the same skip-what-exists
/// semantics as the real store. The concurrent path is covered against SQL Server in
/// <c>NotificationStoreIntegrationTests</c>.
/// </summary>
public sealed class FakeNotificationStore(FakeWaydDbContext dbContext) : INotificationStore
{
    private readonly FakeWaydDbContext _dbContext = dbContext;

    public Task<int> Add(IReadOnlyCollection<Notification> notifications, CancellationToken cancellationToken)
    {
        var added = notifications
            .Where(n => !_dbContext.Notifications.Any(e => e.EventId == n.EventId && e.RecipientEmployeeId == n.RecipientEmployeeId))
            .ToList();

        foreach (var notification in added)
        {
            _dbContext.AddNotification(notification);
        }

        return Task.FromResult(added.Count);
    }

    public Task<bool> MarkRead(Guid recipientEmployeeId, long notificationId, Instant readOn, CancellationToken cancellationToken)
    {
        var mine = _dbContext.Notifications.Where(n => n.Id == notificationId && n.RecipientEmployeeId == recipientEmployeeId).ToList();
        MarkUnread(mine, readOn);
        return Task.FromResult(mine.Count > 0);
    }

    public Task<int> MarkRecordRead(Guid recipientEmployeeId, string ownerType, Guid ownerId, Instant readOn, CancellationToken cancellationToken) =>
        Task.FromResult(MarkUnread(
            _dbContext.Notifications.Where(n => n.RecipientEmployeeId == recipientEmployeeId && n.OwnerType == ownerType && n.OwnerId == ownerId).ToList(),
            readOn));

    public Task<int> MarkAllRead(Guid recipientEmployeeId, Instant readOn, CancellationToken cancellationToken) =>
        Task.FromResult(MarkUnread(
            _dbContext.Notifications.Where(n => n.RecipientEmployeeId == recipientEmployeeId).ToList(),
            readOn));

    public Task<int> DeleteOlderThan(Instant cutoff, int batchSize, CancellationToken cancellationToken)
    {
        var batch = _dbContext.Notifications
            .Where(n => n.OccurredOn < cutoff)
            .OrderBy(n => n.OccurredOn)
            .Take(batchSize)
            .ToList();

        foreach (var notification in batch)
        {
            _dbContext.Notifications.Remove(notification);
        }

        return Task.FromResult(batch.Count);
    }

    /// <summary>Sets <c>ReadOn</c> on the unread ones, as the real store's set-based update does.</summary>
    private static int MarkUnread(List<Notification> notifications, Instant readOn)
    {
        var unread = notifications.Where(n => n.ReadOn is null).ToList();
        foreach (var notification in unread)
        {
            notification.SetPrivate(n => n.ReadOn, readOn);
        }

        return unread.Count;
    }
}
