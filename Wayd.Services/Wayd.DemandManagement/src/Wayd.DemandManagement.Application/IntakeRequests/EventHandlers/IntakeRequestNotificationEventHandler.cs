using Wayd.Common.Application.Notifications;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.Events.Discussions;
using Wayd.Common.Domain.Notifications;
using Wayd.Common.Domain.StatusWorkflows.Enums;
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
/// A message in the request's conversation is told by who it is for. A reply goes to the requester, the assignee and
/// the participants; an internal note to the assignee alone, its followers being held to the notes they may read when
/// the list is read. Either way the people it mentions are told as <see cref="NotificationReason.Mentioned"/>, and a
/// reply in a thread tells everyone who started or replied in that thread as <see cref="NotificationReason.Thread"/>.
/// An edit tells nobody: a mention written in later that makes someone a participant tells them through that.
/// </para>
/// <para>
/// Who may be told of a change that only some readers see — details and participants are substance — is
/// decided when the list is read, by <see cref="IntakeRequestReadRule.VisibleNotifications"/>.
/// </para>
/// <para>
/// A request waiting on its desk unassigned is the desk's, so the desk's triagers are told as
/// <see cref="NotificationReason.Desk"/> when it enters that queue, when its requester adds to it, and when it
/// leaves: created or rerouted in unassigned (the desk it left is told too), changed or replied to by its
/// requester, withdrawn or closed while unassigned, and assigned. Agent work on it is the triagers' own, seen on
/// the board, so it tells them nothing; anyone a message mentions is told as a mention. Which desks are told comes
/// from the event, but whether the request is still unassigned is read as it is now, like every other audience
/// here: a change delivered after the request was taken tells its triagers nothing, since it is no longer theirs.
/// </para>
/// <para>
/// Deletion does not notify: the request is gone, so its notification could never be shown. Creation tells only
/// the triagers, since a submitter knows what they submitted; tags and a switch of workflow tell nobody, being the
/// desk's own filing.
/// </para>
/// <para>
/// The events are delivered durably (see <c>DurableEventRoutes</c>): late, more than once and out of order.
/// The writer keys each notification on the event's id, so a redelivery writes nothing.
/// </para>
/// </remarks>
public sealed class IntakeRequestNotificationEventHandler(
    IDemandManagementDbContext dbContext,
    IDiscussionsDbContext discussionsDbContext,
    INotificationWriter notificationWriter)
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly IDiscussionsDbContext _discussionsDbContext = discussionsDbContext;
    private readonly INotificationWriter _notificationWriter = notificationWriter;

    /// <summary>Tells the desk's triagers a request arrived on it unassigned.</summary>
    public Task Handle(IntakeRequestCreatedEventV2 @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.Created, @event, Audience.TriagersOnly with { TriageDeskIds = [@event.DeskId] },
            cancellationToken);

    /// <summary>
    /// Tells the request's audience an agent moved it to another status, and the desk's triagers when that closed
    /// it unassigned.
    /// </summary>
    public Task Handle(IntakeRequestStatusChangedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.StatusChanged, @event,
            Audience.Everyone with { Triage = IsClosing(@event.FromCategory, @event.ToCategory) ? WhileUnassigned : null },
            cancellationToken);

    /// <summary>Tells the request's audience its requester withdrew it, and the desk's triagers while it is unassigned.</summary>
    public Task Handle(IntakeRequestWithdrawnEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.Withdrawn, @event, Audience.Everyone with { Triage = WhileUnassigned }, cancellationToken);

    /// <summary>Tells the request's audience its title changed, and the desk's triagers when the requester changed it unassigned.</summary>
    public Task Handle(IntakeRequestRetitledEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.Retitled, @event, Audience.Everyone with { Triage = ByRequester(@event.Actor.EmployeeId) },
            cancellationToken);

    /// <summary>
    /// Tells the request's audience its description or expected benefits changed, and the desk's triagers when the
    /// requester changed them unassigned.
    /// </summary>
    public Task Handle(IntakeRequestDetailsUpdatedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.DetailsChanged, @event, Audience.Everyone with { Triage = ByRequester(@event.Actor.EmployeeId) },
            cancellationToken);

    /// <summary>
    /// Tells the request's audience its need-by date changed, and the desk's triagers when the requester changed it
    /// unassigned.
    /// </summary>
    public Task Handle(IntakeRequestNeedByChangedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.TimelineChanged, @event, Audience.Everyone with { Triage = ByRequester(@event.Actor.EmployeeId) },
            cancellationToken);

    /// <summary>
    /// Tells the request's audience, and both the old and the new assignee, that its assignee changed, and the desk's
    /// triagers when someone took it off the queue.
    /// </summary>
    public Task Handle(IntakeRequestAssigneeChangedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.AssigneeChanged, @event,
            Audience.Everyone with
            {
                Assigned = [@event.FromAssigneeId, @event.ToAssigneeId],
                Triage = @event.FromAssigneeId is null && @event.ToAssigneeId is not null ? Always : null,
            },
            cancellationToken);

    /// <summary>
    /// Tells the request's audience it moved to another desk, and, while it is unassigned, the triagers of the desk
    /// it arrived at and of the desk it left.
    /// </summary>
    public Task Handle(IntakeRequestReroutedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.Reparented, @event,
            Audience.Everyone with { Triage = WhileUnassigned, TriageDeskIds = [@event.ToDeskId, @event.FromDeskId] }, cancellationToken);

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

    /// <summary>
    /// Tells the people a message in the request's conversation is for, the people it mentions, and in a thread the
    /// people who started or replied in it.
    /// </summary>
    public async Task Handle(DiscussionMessagePostedEvent @event, CancellationToken cancellationToken)
    {
        if (@event.OwnerId is not { } requestId
            || !string.Equals(@event.ChannelType, DemandManagementDiscussionChannels.IntakeRequest.Key, StringComparison.OrdinalIgnoreCase))
            return;

        IReadOnlyCollection<Guid> thread = @event.ParentMessageId is { } parentId
            ? await _discussionsDbContext.DiscussionMessages
                .Where(m => m.Id == parentId || m.ParentMessageId == parentId)
                .Select(m => m.AuthorEmployeeId)
                .Distinct()
                .ToListAsync(cancellationToken)
            : [];

        var isNote = @event.Kind == DiscussionMessageKind.InternalNote;
        var audience = (isNote ? Audience.AssigneeOnly : Audience.Everyone) with
        {
            Mentioned = @event.MentionedEmployeeIds,
            Thread = thread,
            Triage = isNote ? null : ByRequester(@event.AuthorEmployeeId),
        };

        await Notify(requestId, isNote ? NotificationEvent.InternalNotePosted : NotificationEvent.MessagePosted,
            @event, audience, cancellationToken);
    }

    private async Task Notify(Guid requestId, NotificationEvent notificationEvent, DomainEvent @event, Audience audience, CancellationToken cancellationToken)
    {
        if (!await _notificationWriter.ShouldNotify(@event, cancellationToken))
            return;

        var request = await _dbContext.IntakeRequests
            .Where(r => r.Id == requestId)
            .Select(r => new
            {
                r.DeskId,
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

        if (audience.IncludeAssignee && request.AssigneeId is { } assigneeId)
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
        involved.AddRange(NotificationRecipient.For(NotificationReason.Mentioned, audience.Mentioned ?? []));
        involved.AddRange(NotificationRecipient.For(NotificationReason.Thread, audience.Thread ?? []));

        if (audience.Triage?.Invoke(new TriageFacts(request.RequesterId, request.AssigneeId)) == true)
        {
            // The writer drops inactive employees. Anyone told as the assignee is not also told as a triager:
            // once a request is theirs the desk has dropped out, and muting Assigned must silence it.
            Guid[] deskIds = [.. audience.TriageDeskIds ?? [request.DeskId]];
            var assignedIds = involved.Where(r => r.Reason == NotificationReason.Assigned).Select(r => r.EmployeeId).ToHashSet();
            var triagerIds = await _dbContext.IntakeDesks
                .Where(d => deskIds.Contains(d.Id))
                .SelectMany(d => d.Members.Where(m => m.IsTriager).Select(m => m.EmployeeId))
                .Distinct()
                .ToListAsync(cancellationToken);
            involved.AddRange(NotificationRecipient.For(NotificationReason.Desk, triagerIds.Where(id => !assignedIds.Contains(id))));
        }

        if (involved.Count == 0 && !audience.FollowersAlone)
            return;

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
    /// <param name="Mentioned">People a message mentions.</param>
    /// <param name="Thread">People who started or replied in the thread a message was posted in.</param>
    /// <param name="Triage">
    /// When the desk's triagers are told, decided from the request as it is now; null when they are not.
    /// </param>
    /// <param name="TriageDeskIds">
    /// The desks whose triagers are told, taken from the event so a late or out-of-order delivery tells the desks
    /// the change was about; null for the desk the request is on now.
    /// </param>
    /// <param name="IncludeAssignee">Whether its current assignee is told.</param>
    /// <param name="FollowersAlone">
    /// Whether its followers are told when nobody else is. The writer tells followers whenever it is called, so a
    /// change that is only for the triagers skips it when there are none.
    /// </param>
    private sealed record Audience(
        bool IncludeRequester,
        bool IncludeParticipants,
        IReadOnlyCollection<Guid?> Assigned,
        IReadOnlyCollection<Guid> Participants,
        Guid? RemovedParticipant = null,
        IReadOnlyCollection<Guid>? Mentioned = null,
        IReadOnlyCollection<Guid>? Thread = null,
        Func<TriageFacts, bool>? Triage = null,
        IReadOnlyCollection<Guid>? TriageDeskIds = null,
        bool IncludeAssignee = true,
        bool FollowersAlone = true)
    {
        /// <summary>The requester, the assignee and the participants.</summary>
        public static readonly Audience Everyone = new(true, true, [], []);

        /// <summary>The assignee alone.</summary>
        public static readonly Audience AssigneeOnly = new(false, false, [], []);

        /// <summary>The desk's triagers alone, while the request is unassigned.</summary>
        public static readonly Audience TriagersOnly = new(false, false, [], [], Triage: WhileUnassigned,
            IncludeAssignee: false, FollowersAlone: false);
    }

    /// <summary>What decides whether the desk's triagers are told: who asked for the request, and who has it now.</summary>
    private sealed record TriageFacts(Guid RequesterId, Guid? AssigneeId);

    private static readonly Func<TriageFacts, bool> Always = _ => true;

    private static readonly Func<TriageFacts, bool> WhileUnassigned = facts => facts.AssigneeId is null;

    /// <summary>While the request is unassigned, when the person who acted is its requester.</summary>
    private static Func<TriageFacts, bool> ByRequester(Guid? actorEmployeeId) =>
        facts => facts.AssigneeId is null && actorEmployeeId == facts.RequesterId;

    /// <summary>Whether a status change takes the request out of the open statuses.</summary>
    private static bool IsClosing(StatusCategory from, StatusCategory to) =>
        to is StatusCategory.Done or StatusCategory.Removed && from is not (StatusCategory.Done or StatusCategory.Removed);
}
