using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Requests.ProjectPortfolioManagement;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.DemandProjects.EventHandlers;

/// <summary>
/// Keeps Demand Management's <see cref="DemandProject"/> copy of each PPM project (same Id).
/// </summary>
/// <remarks>
/// The <c>Project*</c> events are delivered durably (see <c>DurableEventRoutes</c>): at least once and in no
/// guaranteed order. This handler follows the rules in "Consuming an event" (docs/contributing/domain-events.mdx):
/// a change older than the one the copy already holds is skipped, a missing copy is created from the
/// project's current state, and a project that no longer exists is left without a copy. A deleted project's
/// copy goes, and the outcomes pointing at it stay as the record of what was decided.
/// </remarks>
public sealed class DemandProjectSyncHandler(
    IDemandManagementDbContext dbContext,
    IDispatcher dispatcher,
    ILogger<DemandProjectSyncHandler> logger)
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ILogger<DemandProjectSyncHandler> _logger = logger;

    public async Task Handle(ProjectCreatedEvent @event, CancellationToken cancellationToken)
    {
        if (await _dbContext.DemandProjects.AnyAsync(x => x.Id == @event.Id, cancellationToken))
        {
            _logger.LogInformation("Demand Management {SystemActionType} for a new Project skipped: Project {ProjectId} already has a copy.", SystemActionType.ServiceDataReplication, @event.Id);
            return;
        }

        await CreateFromSource(@event.Id, @event.Timestamp, cancellationToken);
    }

    public async Task Handle(ProjectDetailsUpdatedEvent @event, CancellationToken cancellationToken)
    {
        await Apply(@event.Id, @event.Timestamp, p => p.ApplyName(@event.Name, @event.Timestamp), "name", cancellationToken);
    }

    public async Task Handle(ProjectKeyChangedEventV2 @event, CancellationToken cancellationToken)
    {
        await Apply(@event.Id, @event.Timestamp, p => p.ApplyKey(@event.Key, @event.Timestamp), "key", cancellationToken);
    }

    public async Task Handle(ProjectDeletedEvent @event, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.DemandProjects.FirstOrDefaultAsync(x => x.Id == @event.Id, cancellationToken);
        if (existing is null)
        {
            _logger.LogInformation("Demand Management {SystemActionType} for a deleted Project skipped: Project {ProjectId} has no copy.", SystemActionType.ServiceDataReplication, @event.Id);
            return;
        }

        _dbContext.DemandProjects.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Demand Management {SystemActionType} for the Project {ProjectId} deleted action.", SystemActionType.ServiceDataReplication, @event.Id);
    }

    private async Task Apply(Guid projectId, Instant timestamp, Func<DemandProject, bool> apply, string change, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.DemandProjects.FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);
        if (existing is null)
        {
            await CreateFromSource(projectId, timestamp, cancellationToken);
            return;
        }

        if (!apply(existing))
        {
            _logger.LogInformation("Demand Management {SystemActionType} for a Project {Change} skipped: Project {ProjectId} already holds this or a newer change.", SystemActionType.ServiceDataReplication, change, projectId);
            return;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Demand Management {SystemActionType} for the Project {ProjectId} {Change} change.", SystemActionType.ServiceDataReplication, projectId, change);
    }

    private async Task CreateFromSource(Guid projectId, Instant timestamp, CancellationToken cancellationToken)
    {
        var source = await _dispatcher.Send(new GetSimpleProjectQuery(projectId), cancellationToken);
        if (source is null)
        {
            _logger.LogInformation("Demand Management {SystemActionType} copy not created: Project {ProjectId} no longer exists.", SystemActionType.ServiceDataReplication, projectId);
            return;
        }

        await _dbContext.DemandProjects.AddAsync(new DemandProject(source, timestamp), cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Demand Management {SystemActionType} creating Project {ProjectId} from its source.", SystemActionType.ServiceDataReplication, projectId);
    }
}
