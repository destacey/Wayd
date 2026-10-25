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
}
