using Wayd.Common.Application.Notifications;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Events.Planning.PlanningIntervalObjectives;
using Wayd.Common.Domain.Notifications;
using Wayd.Planning.Domain;

namespace Wayd.Planning.Application.PlanningIntervals.EventHandlers;

/// <summary>
/// Tells a planning interval objective's followers and team about the changes to it worth hearing about.
/// </summary>
/// <remarks>
/// The participants are the direct members of the team that owns the objective, read when the event is
/// handled. Members of the teams it belongs to or contains are not: an objective is its own team's
/// commitment, and a team of teams would otherwise hear about every one beneath it.
/// <para>
/// Only the events below notify. Progress, order, stretch and description edits are frequent and routine;
/// corrections to a health check, baselines and deletion do not notify either — a deleted objective no
/// longer resolves, so its notification could never be shown.
/// </para>
/// <para>
/// The events are delivered durably (see <c>DurableEventRoutes</c>): late, more than once and out of order.
/// The writer keys each notification on the event's id, so a redelivery writes nothing.
/// </para>
/// </remarks>
public sealed class PlanningIntervalObjectiveNotificationEventHandler(
    IPlanningDbContext planningDbContext,
    IDispatcher dispatcher,
    INotificationWriter notificationWriter)
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly INotificationWriter _notificationWriter = notificationWriter;

    /// <summary>Tells the objective's participants it was created with them in it.</summary>
    public Task Handle(PlanningIntervalObjectiveCreatedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.Created, @event, cancellationToken);

    /// <summary>Tells the objective's audience it moved to a different status.</summary>
    public Task Handle(PlanningIntervalObjectiveStatusChangedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.StatusChanged, @event, cancellationToken);

    /// <summary>Tells the objective's audience a health check was recorded.</summary>
    public Task Handle(PlanningIntervalObjectiveHealthCheckAddedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.HealthReported, @event, cancellationToken);

    /// <summary>Tells the objective's audience its dates changed.</summary>
    public Task Handle(PlanningIntervalObjectiveTimelineChangedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.TimelineChanged, @event, cancellationToken);

    private async Task Notify(Guid objectiveId, NotificationEvent notificationEvent, DomainEvent @event, CancellationToken cancellationToken)
    {
        if (!await _notificationWriter.ShouldNotify(@event, cancellationToken))
            return;

        var teamId = await _planningDbContext.PlanningIntervalObjectives
            .Where(o => o.Id == objectiveId)
            .Select(o => (Guid?)o.TeamId)
            .FirstOrDefaultAsync(cancellationToken);

        // Deleted since the event: nothing written now could ever be shown.
        if (teamId is null)
            return;

        var memberIds = await _dispatcher.Send(new GetTeamMemberIdsQuery(teamId.Value), cancellationToken);

        await _notificationWriter.Notify(
            new RecordChange(PlanningFollowOwners.PlanningIntervalObjective.Key, objectiveId, notificationEvent, @event),
            memberIds,
            cancellationToken);
    }
}
