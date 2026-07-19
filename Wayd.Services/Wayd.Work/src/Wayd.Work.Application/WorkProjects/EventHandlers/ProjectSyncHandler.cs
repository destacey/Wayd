using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkProjects.EventHandlers;

/// <summary>
/// Replicates PPM <c>Project</c> changes into the Work domain's <c>WorkProject</c> projection (same Id).
/// </summary>
/// <remarks>
/// The <c>Project*</c> events are delivered durably (see <c>DurableEventRoutes</c>): enlisted in the
/// Wolverine outbox, delivered on a background thread, and governed by a retry-with-cooldown → dead-letter
/// failure policy. So this handler lets exceptions propagate — a transient DB failure is retried by Wolverine
/// and a poison message dead-letters, rather than being logged-and-dropped. The per-message idempotency
/// guards (create no-ops if the row already exists; update/delete no-op if it does not) make redelivery safe.
/// </remarks>
public sealed class ProjectSyncHandler(IWorkDbContext workDbContext, ILogger<ProjectSyncHandler> logger)
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<ProjectSyncHandler> _logger = logger;

    public async Task Handle(ProjectCreatedEvent @event, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Handling Work {SystemActionType} for a new Project {ProjectId}.", SystemActionType.ServiceDataReplication, @event.Id);
        await CreateProject(@event, cancellationToken);
    }

    public async Task Handle(ProjectDetailsUpdatedEvent @event, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Handling Work {SystemActionType} for an updated Project {ProjectId}.", SystemActionType.ServiceDataReplication, @event.Id);
        await UpdateProject(@event, cancellationToken);
    }

    public async Task Handle(ProjectDeletedEvent @event, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Handling Work {SystemActionType} for a deleted Project {ProjectId}.", SystemActionType.ServiceDataReplication, @event.Id);
        await DeleteProject(@event, cancellationToken);
    }

    private async Task CreateProject(ProjectCreatedEvent createdEvent, CancellationToken cancellationToken)
    {
        // Idempotency guard, not error handling: a redelivery (at-least-once) or a race with the Hangfire
        // SyncWorkProjects bulk path may find the projection already present — that is a success, not a fault.
        if (await _workDbContext.WorkProjects.AnyAsync(x => x.Id == createdEvent.Id, cancellationToken))
        {
            _logger.LogInformation("Work {SystemActionType} for a new Project skipped: Project {ProjectId} already exists in the Work system.", SystemActionType.ServiceDataReplication, createdEvent.Id);
            return;
        }

        var project = new WorkProject(createdEvent);

        await _workDbContext.WorkProjects.AddAsync(project, cancellationToken);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Work {SystemActionType} for the Project {ProjectId} created action.", SystemActionType.ServiceDataReplication, createdEvent.Id);
    }

    private async Task UpdateProject(ProjectDetailsUpdatedEvent updatedEvent, CancellationToken cancellationToken)
    {
        var existingProject = await _workDbContext.WorkProjects
            .FirstOrDefaultAsync(x => x.Id == updatedEvent.Id, cancellationToken);
        if (existingProject == null)
        {
            // The create event has not been applied yet (out-of-order delivery) or the project was deleted.
            // No-op rather than throw: a create redelivery or the Hangfire bulk sync will converge the state.
            _logger.LogWarning("Work {SystemActionType} for an updated Project skipped: Project {ProjectId} does not exist in the Work system.", SystemActionType.ServiceDataReplication, updatedEvent.Id);
            return;
        }

        existingProject.UpdateDetails(updatedEvent);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Work {SystemActionType} for the Project {ProjectId} updated action.", SystemActionType.ServiceDataReplication, updatedEvent.Id);
    }

    private async Task DeleteProject(ProjectDeletedEvent deletedEvent, CancellationToken cancellationToken)
    {
        var existingProject = await _workDbContext.WorkProjects
            .FirstOrDefaultAsync(x => x.Id == deletedEvent.Id, cancellationToken);
        if (existingProject == null)
        {
            // Already gone (redelivery, or the project never replicated) — deleting nothing is the goal state.
            _logger.LogInformation("Work {SystemActionType} for a deleted Project skipped: Project {ProjectId} does not exist in the Work system.", SystemActionType.ServiceDataReplication, deletedEvent.Id);
            return;
        }

        _workDbContext.WorkProjects.Remove(existingProject);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Work {SystemActionType} for the Project {ProjectId} deleted action.", SystemActionType.ServiceDataReplication, deletedEvent.Id);
    }
}
