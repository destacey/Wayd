using Wayd.Common.Application.Notifications;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Discussions;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Notifications;
using Wayd.ProjectPortfolioManagement.Domain;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.EventHandlers;

/// <summary>
/// Tells a project's followers and role holders about the changes to it worth hearing about.
/// </summary>
/// <remarks>
/// Every role holder — sponsor, owner, manager and member — is told for their role, read as the project is when the
/// event is handled. For a roles change that includes the people just added; someone just removed hears of
/// it only if they follow the project.
/// <para>
/// Only the events below notify. Edits to details, key or themes, baselines, corrections to a health check
/// and deletion do not: a deleted project no longer resolves, so its notification could never be shown.
/// </para>
/// <para>
/// A message in the project's conversation also tells the people it mentions, as
/// <see cref="NotificationReason.Mentioned"/>, and in a thread the people who started or replied in it, as
/// <see cref="NotificationReason.Thread"/>. Being told grants nothing: a recipient who cannot read the project is
/// never shown the notification.
/// </para>
/// <para>
/// The events are delivered durably (see <c>DurableEventRoutes</c>): late, more than once and out of order.
/// The writer keys each notification on the event's id, so a redelivery writes nothing.
/// </para>
/// </remarks>
public sealed class ProjectNotificationEventHandler(
    IProjectPortfolioManagementDbContext dbContext,
    IDiscussionsDbContext discussionsDbContext,
    INotificationWriter notificationWriter)
{
    private readonly IProjectPortfolioManagementDbContext _dbContext = dbContext;
    private readonly IDiscussionsDbContext _discussionsDbContext = discussionsDbContext;
    private readonly INotificationWriter _notificationWriter = notificationWriter;

    /// <summary>Tells the project's role holders it was created with them in it.</summary>
    public Task Handle(ProjectCreatedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.Created, @event, cancellationToken);

    /// <summary>Tells the project's audience it moved to a different status.</summary>
    public Task Handle(ProjectStatusChangedEventV2 @event, CancellationToken cancellationToken) =>
        // The origin row a new project starts with is part of its creation, not a change to tell anyone about.
        @event.FromStatus is null
            ? Task.CompletedTask
            : Notify(@event.Id, NotificationEvent.StatusChanged, @event, cancellationToken);

    /// <summary>Tells the project's audience its lifecycle was replaced.</summary>
    public Task Handle(ProjectLifecycleChangedEventV2 @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.LifecycleChanged, @event, cancellationToken);

    /// <summary>Tells the project's audience a health check was recorded.</summary>
    public Task Handle(ProjectHealthCheckAddedEventV2 @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.HealthReported, @event, cancellationToken);

    /// <summary>Tells the project's audience its dates changed.</summary>
    public Task Handle(ProjectTimelineChangedEventV2 @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.TimelineChanged, @event, cancellationToken);

    /// <summary>Tells the project's audience, including anyone just added, that its roles changed.</summary>
    public Task Handle(ProjectRolesChangedEventV2 @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.RolesChanged, @event, cancellationToken);

    /// <summary>Tells the project's audience it moved to a different program or portfolio.</summary>
    public Task Handle(ProjectReparentedEventV2 @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.Reparented, @event, cancellationToken);

    /// <summary>Tells the project's audience it was scored.</summary>
    public Task Handle(ProjectScoreRecordedEventV2 @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.ScoreRecorded, @event, cancellationToken);

    /// <summary>
    /// Tells the project's role holders and followers of a message in its conversation, the people it mentions, and
    /// in a thread the people who started or replied in it.
    /// </summary>
    /// <remarks>
    /// A project holds no internal notes; one is never told, rather than told to an audience no rule defines. An edit
    /// tells nobody.
    /// </remarks>
    public async Task Handle(DiscussionMessagePostedEvent @event, CancellationToken cancellationToken)
    {
        if (@event.OwnerId is not { } projectId
            || @event.Kind != DiscussionMessageKind.Public
            || !string.Equals(@event.ChannelType, PpmDiscussionChannels.Project.Key, StringComparison.OrdinalIgnoreCase))
            return;

        IReadOnlyCollection<Guid> thread = @event.ParentMessageId is { } parentId
            ? await _discussionsDbContext.DiscussionMessages
                .Where(m => m.Id == parentId || m.ParentMessageId == parentId)
                .Select(m => m.AuthorEmployeeId)
                .Distinct()
                .ToListAsync(cancellationToken)
            : [];

        await Notify(projectId, NotificationEvent.MessagePosted, @event, cancellationToken,
        [
            .. NotificationRecipient.For(NotificationReason.Mentioned, @event.MentionedEmployeeIds),
            .. NotificationRecipient.For(NotificationReason.Thread, thread),
        ]);
    }

    private async Task Notify(
        Guid projectId,
        NotificationEvent notificationEvent,
        DomainEvent @event,
        CancellationToken cancellationToken,
        IReadOnlyCollection<NotificationRecipient>? alsoInvolved = null)
    {
        if (!await _notificationWriter.ShouldNotify(@event, cancellationToken))
            return;

        var roleHolderIds = await _dbContext.Projects
            .Where(p => p.Id == projectId)
            .Select(p => p.Roles.Select(r => r.EmployeeId).ToList())
            .FirstOrDefaultAsync(cancellationToken);

        // Deleted since the event: nothing written now could ever be shown.
        if (roleHolderIds is null)
            return;

        await _notificationWriter.Notify(
            new RecordChange(PpmFollowOwners.Project.Key, projectId, notificationEvent, @event),
            [.. NotificationRecipient.For(NotificationReason.Role, roleHolderIds), .. alsoInvolved ?? []],
            cancellationToken);
    }
}
