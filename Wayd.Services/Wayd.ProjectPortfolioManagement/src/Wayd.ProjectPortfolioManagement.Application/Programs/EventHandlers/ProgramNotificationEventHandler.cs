using Wayd.Common.Application.Notifications;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Notifications;
using Wayd.ProjectPortfolioManagement.Domain;

namespace Wayd.ProjectPortfolioManagement.Application.Programs.EventHandlers;

/// <summary>
/// Tells a program's followers and role holders about the changes to it worth hearing about.
/// </summary>
/// <remarks>
/// Every role holder — sponsor, owner and manager — is told for their role, read as the program is when the event
/// is handled. For a roles change that includes the people just added; someone just removed hears of it only
/// if they follow the program.
/// <para>
/// Only the events below notify. Edits to details or themes, baselines and deletion do not: a deleted program
/// no longer resolves, so its notification could never be shown.
/// </para>
/// <para>
/// The events are delivered durably (see <c>DurableEventRoutes</c>): late, more than once and out of order.
/// The writer keys each notification on the event's id, so a redelivery writes nothing.
/// </para>
/// </remarks>
public sealed class ProgramNotificationEventHandler(
    IProjectPortfolioManagementDbContext dbContext,
    INotificationWriter notificationWriter)
{
    private readonly IProjectPortfolioManagementDbContext _dbContext = dbContext;
    private readonly INotificationWriter _notificationWriter = notificationWriter;

    /// <summary>Tells the program's role holders it was created with them in it.</summary>
    public Task Handle(ProgramCreatedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.Created, @event, cancellationToken);

    /// <summary>Tells the program's audience it moved to a different status.</summary>
    public Task Handle(ProgramStatusChangedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.StatusChanged, @event, cancellationToken);

    /// <summary>Tells the program's audience its dates changed.</summary>
    public Task Handle(ProgramTimelineChangedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.TimelineChanged, @event, cancellationToken);

    /// <summary>Tells the program's audience, including anyone just added, that its roles changed.</summary>
    public Task Handle(ProgramRolesChangedEvent @event, CancellationToken cancellationToken) =>
        Notify(@event.Id, NotificationEvent.RolesChanged, @event, cancellationToken);

    private async Task Notify(Guid programId, NotificationEvent notificationEvent, DomainEvent @event, CancellationToken cancellationToken)
    {
        if (!await _notificationWriter.ShouldNotify(@event, cancellationToken))
            return;

        var roleHolderIds = await _dbContext.Programs
            .Where(p => p.Id == programId)
            .Select(p => p.Roles.Select(r => r.EmployeeId).ToList())
            .FirstOrDefaultAsync(cancellationToken);

        // Deleted since the event: nothing written now could ever be shown.
        if (roleHolderIds is null)
            return;

        await _notificationWriter.Notify(
            new RecordChange(PpmFollowOwners.Program.Key, programId, notificationEvent, @event),
            NotificationRecipient.For(NotificationReason.Role, roleHolderIds).ToList(),
            cancellationToken);
    }
}
