using Wayd.Common.Application.Notifications;
using Wayd.Common.Domain.Events;
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
/// The events are delivered durably (see <c>DurableEventRoutes</c>): late, more than once and out of order.
/// The writer keys each notification on the event's id, so a redelivery writes nothing.
/// </para>
/// </remarks>
public sealed class IncidentNotificationEventHandler(
    IProductManagementDbContext productManagementDbContext,
    INotificationWriter notificationWriter)
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly INotificationWriter _notificationWriter = notificationWriter;

    public Task Handle(IncidentCreatedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.Created, @event, cancellationToken);

    public Task Handle(IncidentStatusChangedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.StatusChanged, @event, cancellationToken);

    public Task Handle(IncidentTimelineChangedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.TimelineChanged, @event, cancellationToken);

    public Task Handle(IncidentTimelineNoteAddedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.MessagePosted, @event, cancellationToken);

    private async Task Notify(
        Guid incidentId,
        NotificationEvent notificationEvent,
        DomainEvent @event,
        CancellationToken cancellationToken)
    {
        if (!await _notificationWriter.ShouldNotify(@event, cancellationToken))
        {
            return;
        }

        var commanderId = await _productManagementDbContext.Incidents
            .Where(i => i.Id == incidentId)
            .Select(i => (Guid?)i.CommanderEmployeeId)
            .FirstOrDefaultAsync(cancellationToken);

        if (commanderId is null)
        {
            return;
        }

        await _notificationWriter.Notify(
            new RecordChange(ProductManagementFollowOwners.Incident.Key, incidentId, notificationEvent, @event),
            NotificationRecipient.For(NotificationReason.Role, [commanderId.Value]).ToList(),
            cancellationToken);
    }
}
