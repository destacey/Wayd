using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Application.Notifications;

/// <summary>
/// Resolves who should hear about a change and writes their notifications.
/// </summary>
/// <remarks>
/// The audience is the record's followers plus the participants its module names, merged so each person
/// gets one notification with every reason that applies. From that audience it removes:
/// <list type="bullet">
/// <item>the person who made the change — nobody is told about their own action;</item>
/// <item>employees who are deleted or inactive — both mean the person has left, and their follows and roles
/// outlive them;</item>
/// </list>
/// and writes nothing at all for a change no person made directly. Imports, syncs and the platform's own
/// work change records in bulk; telling everyone about each row would bury the changes people made.
/// <para>
/// Whether a recipient may read the record is not asked here. That is decided every time the list is read,
/// through the record's read rule, because access can change after a notification is written.
/// </para>
/// </remarks>
public sealed class NotificationWriter(
    INotificationsDbContext notificationsDbContext,
    IWaydDbContext waydDbContext,
    INotificationStore notificationStore,
    IUserService userService,
    IFeatureFlags featureFlags,
    ILogger<NotificationWriter> logger)
    : INotificationWriter
{
    private readonly INotificationsDbContext _notificationsDbContext = notificationsDbContext;
    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly INotificationStore _notificationStore = notificationStore;
    private readonly IUserService _userService = userService;
    private readonly IFeatureFlags _featureFlags = featureFlags;
    private readonly ILogger<NotificationWriter> _logger = logger;

    /// <inheritdoc/>
    public async Task<int> Notify(RecordChange change, IReadOnlyCollection<Guid> participantEmployeeIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(participantEmployeeIds);

        if (!await ShouldNotify(change.Source, cancellationToken))
        {
            return 0;
        }

        var actor = change.Source.Actor;

        var audience = new Dictionary<Guid, NotificationReason>();

        var followerIds = await _notificationsDbContext.Follows
            .Where(f => f.OwnerType == change.OwnerType && f.OwnerId == change.OwnerId)
            .Select(f => f.EmployeeId)
            .ToListAsync(cancellationToken);
        Add(audience, followerIds, NotificationReason.Following);
        Add(audience, participantEmployeeIds, NotificationReason.Participant);

        var actorEmployeeId = actor.EmployeeId ?? await ActorEmployeeId(actor, cancellationToken);
        if (actorEmployeeId is { } actorId)
        {
            audience.Remove(actorId);
        }

        if (audience.Count == 0)
        {
            return 0;
        }

        var candidateIds = audience.Keys.ToList();
        var currentIds = await _waydDbContext.Employees
            .Where(e => candidateIds.Contains(e.Id) && e.IsActive && !e.IsDeleted)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
        if (currentIds.Count == 0)
        {
            return 0;
        }

        var notifications = new List<Notification>(currentIds.Count);
        foreach (var recipientId in currentIds)
        {
            var notification = Notification.Create(
                recipientId,
                change.OwnerType,
                change.OwnerId,
                change.Source.EventId,
                change.Event,
                actor.Kind,
                actorEmployeeId,
                change.Source.Timestamp,
                audience[recipientId]);

            // Every input is fixed by the handler that called, so a failure is a bug there, not bad data.
            if (notification.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Could not notify employee {recipientId} of event {change.Source.EventId}: {notification.Error}");
            }

            notifications.Add(notification.Value);
        }

        var added = await _notificationStore.Add(notifications, cancellationToken);

        _logger.LogDebug(
            "Wrote {Count} notifications of {NotificationEvent} on {OwnerType} {OwnerId} for event {EventId}.",
            added, change.Event, change.OwnerType, change.OwnerId, change.Source.EventId);

        return added;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// An automation is attributed to whoever set it off, which an import or sync with a known operator also
    /// does: a reaction to an imported row arrives here as <see cref="EventActorKind.Automation"/>. None of the
    /// events that notify today is raised that way; one that is would notify followers once per imported row.
    /// </remarks>
    public async Task<bool> ShouldNotify(DomainEvent source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Actor.Kind is not (EventActorKind.User or EventActorKind.Automation))
        {
            return false;
        }

        // Notifications ship with following: nothing reads them while the flag is off.
        return await _featureFlags.IsEnabled(FeatureFlags.Names.Following, cancellationToken);
    }

    private static void Add(Dictionary<Guid, NotificationReason> audience, IEnumerable<Guid> employeeIds, NotificationReason reason)
    {
        foreach (var employeeId in employeeIds)
        {
            audience[employeeId] = audience.TryGetValue(employeeId, out var reasons) ? reasons | reason : reason;
        }
    }

    /// <summary>The employee linked to the account that made the change, for an actor that did not carry one.</summary>
    private async Task<Guid?> ActorEmployeeId(EventActor actor, CancellationToken cancellationToken) =>
        string.IsNullOrEmpty(actor.UserId)
            ? null
            : await _userService.GetEmployeeIdAsync(actor.UserId, cancellationToken);
}
