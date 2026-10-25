namespace Wayd.Common.Application.Notifications.Commands;

/// <summary>
/// Deletes every notification about a change older than the retention window, read or not, answering how many
/// were deleted.
/// </summary>
/// <remarks>
/// Notifications are a per-person view, not a record of what happened: the activity log and audit trail
/// keep that, so a notification can go once it is too old to be news without anything being lost.
/// </remarks>
public sealed record PurgeExpiredNotificationsCommand : ICommand<int>, ILongRunningRequest;

/// <summary>Deletes expired notifications in batches.</summary>
public sealed class PurgeExpiredNotificationsCommandHandler(
    INotificationStore notificationStore,
    IDateTimeProvider dateTimeProvider,
    ILogger<PurgeExpiredNotificationsCommandHandler> logger)
    : ICommandHandler<PurgeExpiredNotificationsCommand, int>
{
    /// <summary>How long a notification is kept after the change it describes.</summary>
    public static readonly Duration Retention = Duration.FromDays(90);

    /// <summary>The most notifications one delete removes.</summary>
    public const int BatchSize = 5000;

    private readonly INotificationStore _notificationStore = notificationStore;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<PurgeExpiredNotificationsCommandHandler> _logger = logger;

    public async Task<Result<int>> Handle(PurgeExpiredNotificationsCommand command, CancellationToken cancellationToken)
    {
        // Fixed once, so notifications written while the sweep runs are never in reach of it.
        var cutoff = _dateTimeProvider.Now - Retention;

        var purged = 0;
        int deleted;
        do
        {
            deleted = await _notificationStore.DeleteOlderThan(cutoff, BatchSize, cancellationToken);
            purged += deleted;
        }
        while (deleted == BatchSize);

        if (purged > 0)
            _logger.LogInformation("Notification retention sweep deleted {Count} notification(s) older than {Retention}.", purged, Retention);

        return Result.Success(purged);
    }
}
