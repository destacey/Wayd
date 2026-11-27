using Wayd.Common.Application.Notifications;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Discussions;
using Wayd.Common.Domain.Events.ProductManagement.Incidents;
using Wayd.Common.Domain.Notifications;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Incidents.EventHandlers;

/// <summary>
/// Tells an incident's followers and commander about the changes worth hearing about.
/// </summary>
/// <remarks>
/// Only the events below notify. Detail edits that do not move the timeline do not notify; corrections
/// and deletion do not either.
/// <para>
/// A message in the incident's discussion also tells the people it mentions, as
/// <see cref="NotificationReason.Mentioned"/>, and in a thread the people who started or replied in it, as
/// <see cref="NotificationReason.Thread"/>. Being told grants nothing: a recipient who cannot read the incident is
/// never shown the notification.
/// </para>
/// <para>
/// The events are delivered durably (see <c>DurableEventRoutes</c>): late, more than once and out of order.
/// The writer keys each notification on the event's id, so a redelivery writes nothing.
/// </para>
/// </remarks>
public sealed class IncidentNotificationEventHandler(
    IProductManagementDbContext productManagementDbContext,
    IDiscussionsDbContext discussionsDbContext,
    INotificationWriter notificationWriter)
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IDiscussionsDbContext _discussionsDbContext = discussionsDbContext;
    private readonly INotificationWriter _notificationWriter = notificationWriter;

    /// <summary>Tells the incident's commander it was opened with them in command.</summary>
    public Task Handle(IncidentCreatedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.Created, @event, cancellationToken);

    /// <summary>Tells the incident's audience it moved to a different status.</summary>
    public Task Handle(IncidentStatusChangedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.StatusChanged, @event, cancellationToken);

    /// <summary>Tells the incident's audience when it was noticed, responded to or recovered changed.</summary>
    public Task Handle(IncidentTimelineChangedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.TimelineChanged, @event, cancellationToken);

    /// <summary>Tells the incident's audience a note was added to its operational timeline.</summary>
    public Task Handle(IncidentTimelineNoteAddedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.MessagePosted, @event, cancellationToken);

    /// <summary>
    /// Tells the incident's commander and followers of a message in its discussion, the people it mentions, and in a
    /// thread the people who started or replied in it.
    /// </summary>
    /// <remarks>
    /// An incident's discussion holds no internal notes; one is never told, rather than told to an audience no rule
    /// defines. An edit tells nobody.
    /// </remarks>
    public async Task Handle(DiscussionMessagePostedEvent @event, CancellationToken cancellationToken)
    {
        if (@event.OwnerId is not { } incidentId
            || @event.Kind != DiscussionMessageKind.Public
            || !string.Equals(@event.ChannelType, ProductManagementDiscussionChannels.Incident.Key, StringComparison.OrdinalIgnoreCase))
            return;

        IReadOnlyCollection<Guid> thread = @event.ParentMessageId is { } parentId
            ? await _discussionsDbContext.DiscussionMessages
                .Where(m => m.Id == parentId || m.ParentMessageId == parentId)
                .Select(m => m.AuthorEmployeeId)
                .Distinct()
                .ToListAsync(cancellationToken)
            : [];

        await Notify(incidentId, NotificationEvent.MessagePosted, @event, cancellationToken,
        [
            .. NotificationRecipient.For(NotificationReason.Mentioned, @event.MentionedEmployeeIds),
            .. NotificationRecipient.For(NotificationReason.Thread, thread),
        ]);
    }

    private async Task Notify(
        Guid incidentId,
        NotificationEvent notificationEvent,
        DomainEvent @event,
        CancellationToken cancellationToken,
        IReadOnlyCollection<NotificationRecipient>? alsoInvolved = null)
    {
        if (!await _notificationWriter.ShouldNotify(@event, cancellationToken))
        {
            return;
        }

        var row = await _productManagementDbContext.Incidents
            .Where(i => i.Id == incidentId)
            .Select(i => new { i.CommanderEmployeeId })
            .FirstOrDefaultAsync(cancellationToken);

        // Deleted since the event: nothing written now could ever be shown.
        if (row is null)
        {
            return;
        }

        List<NotificationRecipient> involved = row.CommanderEmployeeId is Guid commanderId
            ? [new(commanderId, NotificationReason.Role)]
            : [];
        involved.AddRange(alsoInvolved ?? []);

        await _notificationWriter.Notify(
            new RecordChange(ProductManagementFollowOwners.Incident.Key, incidentId, notificationEvent, @event),
            involved,
            cancellationToken);
    }
}
