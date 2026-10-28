using Wayd.Common.Application.Notifications;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.Notifications;
using Wayd.DemandManagement.Domain;

namespace Wayd.DemandManagement.Application.IntakeRequests.EventHandlers;

/// <summary>
/// Tells the people involved in an intake request about the changes to it, each for their own reason.
/// </summary>
/// <remarks>
/// The audience is read from the request as it is when the event is handled, never kept as subscriptions, so a
/// reassignment or a reroute can never leave someone hearing about a request that is no longer theirs:
/// <list type="bullet">
/// <item>its requester, as <see cref="NotificationReason.Requester"/>;</item>
/// <item>its assignee, as <see cref="NotificationReason.Assigned"/> — for an assignee change, the one it had
/// before as well as the one it has now;</item>
/// <item>its participants and, when it was entered on the requester's behalf, its submitter, as
/// <see cref="NotificationReason.Participant"/>;</item>
/// <item>its followers, whom the writer adds.</item>
/// </list>
/// A participant being added or removed is told to the assignee and the person added or removed, not the
/// requester or the other participants. The person removed is told only on an <c>Open</c> desk: anywhere else
/// they can no longer read who takes part in the request, so the notification could never be shown to them. A channel change is told to the requester, who sees the channel, but
/// not to the participants.
/// <para>
/// Who may be told of a change that only some readers see — details and participants are substance — is
/// decided when the list is read, by <see cref="IntakeRequestReadRule.VisibleNotifications"/>.
/// </para>
/// <para>
/// Deletion does not notify: the request is gone, so its notification could never be shown. Creation, tags and
/// a switch of workflow do not either: a submitter knows what they submitted, and tags and workflows are the
/// desk's own filing.
/// </para>
/// <para>
/// The events are delivered durably (see <c>DurableEventRoutes</c>): late, more than once and out of order.
/// The writer keys each notification on the event's id, so a redelivery writes nothing.
/// </para>
/// </remarks>
public sealed class IntakeRequestNotificationEventHandler(
    IDemandManagementDbContext dbContext,
    INotificationWriter notificationWriter)
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly INotificationWriter _notificationWriter = notificationWriter;

    /// <summary>Tells the request's audience an agent moved it to another status.</summary>
    public Task Handle(IntakeRequestStatusChangedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.StatusChanged, @event, Audience.Everyone, cancellationToken);

    /// <summary>Tells the request's audience its requester withdrew it.</summary>
    public Task Handle(IntakeRequestWithdrawnEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.Withdrawn, @event, Audience.Everyone, cancellationToken);

    /// <summary>Tells the request's audience its title changed.</summary>
    public Task Handle(IntakeRequestRetitledEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.Retitled, @event, Audience.Everyone, cancellationToken);

    /// <summary>Tells the request's audience its description or expected benefits changed.</summary>
    public Task Handle(IntakeRequestDetailsUpdatedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.DetailsChanged, @event, Audience.Everyone, cancellationToken);

    /// <summary>Tells the request's audience its need-by date changed.</summary>
    public Task Handle(IntakeRequestNeedByChangedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.TimelineChanged, @event, Audience.Everyone, cancellationToken);

    /// <summary>Tells the request's audience, and both the old and the new assignee, that its assignee changed.</summary>
    public Task Handle(IntakeRequestAssigneeChangedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.AssigneeChanged, @event,
            Audience.Everyone with { Assigned = [@event.FromAssigneeId, @event.ToAssigneeId] }, cancellationToken);

    /// <summary>Tells the request's audience it moved to another desk.</summary>
    public Task Handle(IntakeRequestReroutedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.Reparented, @event, Audience.Everyone, cancellationToken);

    /// <summary>Tells the assignee, and the person added, that someone was given access to the request.</summary>
    public Task Handle(IntakeRequestParticipantAddedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.ParticipantsChanged, @event,
            Audience.AssigneeOnly with { Participants = [@event.EmployeeId] }, cancellationToken);

    /// <summary>Tells the assignee, and on an open desk the person removed, that someone lost access to the request.</summary>
    public Task Handle(IntakeRequestParticipantRemovedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.ParticipantsChanged, @event,
            Audience.AssigneeOnly with { RemovedParticipant = @event.EmployeeId }, cancellationToken);

    /// <summary>Tells the requester and the assignee that where the request came from changed.</summary>
    public Task Handle(IntakeRequestChannelChangedEventV2 @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.ChannelChanged, @event,
            Audience.AssigneeOnly with { IncludeRequester = true }, cancellationToken);

    private async Task Notify(Guid requestId, NotificationEvent notificationEvent, DomainEvent @event, Audience audience, CancellationToken cancellationToken)
    {
        if (!await _notificationWriter.ShouldNotify(@event, cancellationToken))
            return;

        var request = await _dbContext.IntakeRequests
            .Where(r => r.Id == requestId)
            .Select(r => new
            {
                r.RequesterId,
                r.SubmittedById,
                r.AssigneeId,
                ParticipantIds = r.Participants.Select(p => p.EmployeeId).ToList(),
                DeskVisibility = _dbContext.IntakeDesks.Where(d => d.Id == r.DeskId).Select(d => (IntakeDeskVisibility?)d.Visibility).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Deleted since the event: nothing written now could ever be shown.
        if (request is null)
            return;

        List<NotificationRecipient> involved = [];

        if (audience.IncludeRequester)
            involved.Add(new(request.RequesterId, NotificationReason.Requester));

        if (request.AssigneeId is { } assigneeId)
            involved.Add(new(assigneeId, NotificationReason.Assigned));
        foreach (var id in audience.Assigned.OfType<Guid>())
            involved.Add(new(id, NotificationReason.Assigned));

        if (audience.IncludeParticipants)
        {
            involved.AddRange(NotificationRecipient.For(NotificationReason.Participant, request.ParticipantIds));
            if (request.SubmittedById != request.RequesterId)
                involved.Add(new(request.SubmittedById, NotificationReason.Participant));
        }
        involved.AddRange(NotificationRecipient.For(NotificationReason.Participant, audience.Participants));
        if (audience.RemovedParticipant is { } removedId && request.DeskVisibility == IntakeDeskVisibility.Open)
            involved.Add(new(removedId, NotificationReason.Participant));

        await _notificationWriter.Notify(
            new RecordChange(DemandManagementFollowOwners.IntakeRequest.Key, requestId, notificationEvent, @event),
            involved,
            cancellationToken);
    }

    /// <summary>Which of the request's people a change is told to, beyond its current assignee and followers.</summary>
    /// <param name="IncludeRequester">Whether its requester is told.</param>
    /// <param name="IncludeParticipants">Whether its participants and submitter are told.</param>
    /// <param name="Assigned">People told as assignees besides the current one, such as the one it had before.</param>
    /// <param name="Participants">People told as participants besides the current ones, such as one just added.</param>
    /// <param name="RemovedParticipant">A participant just removed, told only while the request's desk is open.</param>
    private sealed record Audience(
        bool IncludeRequester,
        bool IncludeParticipants,
        IReadOnlyCollection<Guid?> Assigned,
        IReadOnlyCollection<Guid> Participants,
        Guid? RemovedParticipant = null)
    {
        /// <summary>The requester, the assignee and the participants.</summary>
        public static readonly Audience Everyone = new(true, true, [], []);

        /// <summary>The assignee alone.</summary>
        public static readonly Audience AssigneeOnly = new(false, false, [], []);
    }
}
