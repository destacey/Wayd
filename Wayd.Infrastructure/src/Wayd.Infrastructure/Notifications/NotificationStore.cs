using Microsoft.EntityFrameworkCore;
using NodaTime;
using Wayd.Common.Application.Notifications;
using Wayd.Common.Domain.Notifications;
using Wayd.Infrastructure.Persistence.Extensions;

namespace Wayd.Infrastructure.Notifications;

/// <summary>
/// Writes notifications against SQL Server, where the unique key on (event, recipient) is what stops a
/// concurrent redelivery of one event from telling anyone twice.
/// </summary>
internal sealed class NotificationStore(WaydDbContext dbContext) : INotificationStore
{
    /// <summary>How many times one event's notifications are tried before the failure is left to the durable retry.</summary>
    private const int MaxAttempts = 3;

    private readonly WaydDbContext _dbContext = dbContext;

    /// <inheritdoc/>
    public async Task<int> Add(IReadOnlyCollection<Notification> notifications, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notifications);
        if (notifications.Count == 0)
        {
            return 0;
        }

        var eventId = notifications.First().EventId;
        if (notifications.Any(n => n.EventId != eventId))
        {
            throw new ArgumentException("Notifications are added one event at a time.", nameof(notifications));
        }

        // A concurrent delivery of the same event may commit first, and its audience can differ from this
        // one's — a follow added between the two reads. Losing the race therefore only means some recipients
        // are already written: re-read them and write the rest. A third collision means unusual contention,
        // and the durable retry takes it from there.
        for (var attempt = 1; ; attempt++)
        {
            var existing = await _dbContext.Notifications
                .Where(n => n.EventId == eventId)
                .Select(n => n.RecipientEmployeeId)
                .ToListAsync(cancellationToken);

            var added = notifications.Where(n => !existing.Contains(n.RecipientEmployeeId)).ToList();
            if (added.Count == 0)
            {
                return 0;
            }

            _dbContext.Notifications.AddRange(added);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return added.Count;
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation() && attempt < MaxAttempts)
            {
                foreach (var notification in added)
                {
                    _dbContext.Entry(notification).State = EntityState.Detached;
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<bool> MarkRead(Guid recipientEmployeeId, long notificationId, Instant readOn, CancellationToken cancellationToken)
    {
        await _dbContext.Notifications
            .Where(n => n.Id == notificationId && n.RecipientEmployeeId == recipientEmployeeId && n.ReadOn == null)
            .ExecuteUpdateAsync(u => u.SetProperty(n => n.ReadOn, readOn), cancellationToken);

        return await _dbContext.Notifications
            .AnyAsync(n => n.Id == notificationId && n.RecipientEmployeeId == recipientEmployeeId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<int> MarkRecordRead(Guid recipientEmployeeId, string ownerType, Guid ownerId, Instant readOn, CancellationToken cancellationToken) =>
        _dbContext.Notifications
            .Where(n => n.RecipientEmployeeId == recipientEmployeeId
                && n.OwnerType == ownerType
                && n.OwnerId == ownerId
                && n.ReadOn == null)
            .ExecuteUpdateAsync(u => u.SetProperty(n => n.ReadOn, readOn), cancellationToken);

    /// <inheritdoc/>
    public Task<int> MarkAllRead(Guid recipientEmployeeId, Instant readOn, CancellationToken cancellationToken) =>
        _dbContext.Notifications
            .Where(n => n.RecipientEmployeeId == recipientEmployeeId && n.ReadOn == null)
            .ExecuteUpdateAsync(u => u.SetProperty(n => n.ReadOn, readOn), cancellationToken);
}
