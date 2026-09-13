using Wayd.Common.Application.Requests.ProjectPortfolioManagement;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkProjects.EventHandlers;

/// <summary>
/// Keeps the Work module's <c>WorkProject</c> copy of each PPM project (same Id).
/// </summary>
/// <remarks>
/// The <c>Project*</c> events are delivered durably (see <c>DurableEventRoutes</c>): at least once and in no
/// guaranteed order, with a retry-with-cooldown → dead-letter failure policy. This handler follows the rules
/// in "Consuming an event" (docs/contributing/domain-events.mdx): a change older than the one the copy already
/// holds is skipped, a missing copy is created from the project's current state, and a project that no longer
/// exists is left without a copy.
/// </remarks>
public sealed class ProjectSyncHandler(IWorkDbContext workDbContext, IDispatcher dispatcher, ILogger<ProjectSyncHandler> logger)
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ILogger<ProjectSyncHandler> _logger = logger;

    public async Task Handle(ProjectCreatedEvent @event, CancellationToken cancellationToken)
    {
        if (await _workDbContext.WorkProjects.AnyAsync(x => x.Id == @event.Id, cancellationToken))
        {
            _logger.LogInformation("Work {SystemActionType} for a new Project skipped: Project {ProjectId} already has a copy.", SystemActionType.ServiceDataReplication, @event.Id);
            return;
        }

        await CreateFromSource(@event.Id, @event.Timestamp, cancellationToken);
    }

    public async Task Handle(ProjectDetailsUpdatedEvent @event, CancellationToken cancellationToken)
    {
        await Apply(@event.Id, @event.Timestamp, p => p.ApplyDetails(@event.Name, @event.Description, @event.Timestamp), "details", cancellationToken);
    }

    public async Task Handle(ProjectKeyChangedEventV2 @event, CancellationToken cancellationToken)
    {
        await Apply(@event.Id, @event.Timestamp, p => p.ApplyKey(@event.Key, @event.Timestamp), "key", cancellationToken);
    }

    // Nothing raises the superseded type, but an envelope written as it before the switch can still be
    // waiting in the durable outbox; without this it would dead-letter rather than rekey the copy.
#pragma warning disable CS0618
    public async Task Handle(ProjectKeyChangedEvent @event, CancellationToken cancellationToken)
#pragma warning restore CS0618
    {
        await Apply(@event.Id, @event.Timestamp, p => p.ApplyKey(@event.Key, @event.Timestamp), "key", cancellationToken);
    }

    public async Task Handle(ProjectDeletedEvent @event, CancellationToken cancellationToken)
    {
        var existingProject = await _workDbContext.WorkProjects
            .FirstOrDefaultAsync(x => x.Id == @event.Id, cancellationToken);
        if (existingProject == null)
        {
            _logger.LogInformation("Work {SystemActionType} for a deleted Project skipped: Project {ProjectId} has no copy.", SystemActionType.ServiceDataReplication, @event.Id);
            return;
        }

        _workDbContext.WorkProjects.Remove(existingProject);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Work {SystemActionType} for the Project {ProjectId} deleted action.", SystemActionType.ServiceDataReplication, @event.Id);
    }

    private async Task Apply(Guid projectId, Instant timestamp, Func<WorkProject, bool> apply, string change, CancellationToken cancellationToken)
    {
        var existingProject = await _workDbContext.WorkProjects
            .FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);
        if (existingProject == null)
        {
            await CreateFromSource(projectId, timestamp, cancellationToken);
            return;
        }

        if (!apply(existingProject))
        {
            _logger.LogInformation("Work {SystemActionType} for a Project {Change} skipped: Project {ProjectId} already holds this or a newer change.", SystemActionType.ServiceDataReplication, change, projectId);
            return;
        }

        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Work {SystemActionType} for the Project {ProjectId} {Change} change.", SystemActionType.ServiceDataReplication, projectId, change);
    }

    private async Task CreateFromSource(Guid projectId, Instant timestamp, CancellationToken cancellationToken)
    {
        var source = await _dispatcher.Send(new GetSimpleProjectQuery(projectId), cancellationToken);
        if (source is null)
        {
            _logger.LogInformation("Work {SystemActionType} copy not created: Project {ProjectId} no longer exists.", SystemActionType.ServiceDataReplication, projectId);
            return;
        }

        await _workDbContext.WorkProjects.AddAsync(new WorkProject(source, timestamp), cancellationToken);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Work {SystemActionType} creating Project {ProjectId} from its source.", SystemActionType.ServiceDataReplication, projectId);
    }
}
