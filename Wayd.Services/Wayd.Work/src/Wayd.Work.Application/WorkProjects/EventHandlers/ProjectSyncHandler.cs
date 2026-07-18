using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkProjects.EventHandlers;

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
        try
        {
            if (await _workDbContext.WorkProjects.AnyAsync(x => x.Id == createdEvent.Id, cancellationToken))
            {
                _logger.LogCritical("Error processing Work {SystemActionType} for a new Project. Project {ProjectId} already exists in the Work system.", SystemActionType.ServiceDataReplication, createdEvent.Id);
                return;
            }
            var project = new WorkProject(createdEvent);

            await _workDbContext.WorkProjects.AddAsync(project, cancellationToken);
            await _workDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successful Work {SystemActionType} for the Project {ProjectId} created action.", SystemActionType.ServiceDataReplication, createdEvent.Id);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Exception handling Work {SystemActionType} for the Project {ProjectId} created action.", SystemActionType.ServiceDataReplication, createdEvent.Id);
        }
    }

    private async Task UpdateProject(ProjectDetailsUpdatedEvent updatedEvent, CancellationToken cancellationToken)
    {
        try
        {
            var existingProject = await _workDbContext.WorkProjects
                .FirstOrDefaultAsync(x => x.Id == updatedEvent.Id, cancellationToken);
            if (existingProject == null)
            {
                _logger.LogCritical("Error processing Work {SystemActionType} for an updated Project. Project {ProjectId} does not exist in the Work system.", SystemActionType.ServiceDataReplication, updatedEvent.Id);
                return;
            }
            existingProject.UpdateDetails(updatedEvent);
            await _workDbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Successful Work {SystemActionType} for the Project {ProjectId} updated action.", SystemActionType.ServiceDataReplication, updatedEvent.Id);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Exception handling Work {SystemActionType} for the Project {ProjectId} updated action.", SystemActionType.ServiceDataReplication, updatedEvent.Id);
        }
    }

    private async Task DeleteProject(ProjectDeletedEvent deletedEvent, CancellationToken cancellationToken)
    {
        try
        {
            var existingProject = await _workDbContext.WorkProjects
                .FirstOrDefaultAsync(x => x.Id == deletedEvent.Id, cancellationToken);
            if (existingProject == null)
            {
                _logger.LogCritical("Error processing Work {SystemActionType} for a deleted Project. Project {ProjectId} does not exist in the Work system.", SystemActionType.ServiceDataReplication, deletedEvent.Id);
                return;
            }

            _workDbContext.WorkProjects.Remove(existingProject);
            await _workDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successful Work {SystemActionType} for the Project {ProjectId} deleted action.", SystemActionType.ServiceDataReplication, deletedEvent.Id);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Exception handling Work {SystemActionType} for the Project {ProjectId} deleted action.", SystemActionType.ServiceDataReplication, deletedEvent.Id);
        }
    }
}