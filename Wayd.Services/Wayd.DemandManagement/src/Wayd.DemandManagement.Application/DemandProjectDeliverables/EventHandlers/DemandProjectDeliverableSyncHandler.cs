using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Requests.ProjectPortfolioManagement;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Events.StatusWorkflows;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.DemandProjectDeliverables.EventHandlers;

/// <summary>
/// Keeps Demand Management's <see cref="DemandProjectDeliverable"/> copy of each PPM project deliverable (same Id).
/// </summary>
/// <remarks>
/// The <c>ProjectDeliverable*</c> events and <see cref="StatusWorkflowSwitchedEvent"/> are delivered durably (see
/// <c>DurableEventRoutes</c>): at least once and in no guaranteed order. This handler follows the rules in
/// "Consuming an event" (docs/contributing/domain-events.mdx): a change older than the one the copy already
/// holds is skipped, a missing copy is created from the deliverable's current state, and a deliverable that no
/// longer exists is left without a copy. A copy needs its project's copy, so building one builds that too when
/// the project's own events have not arrived yet. A deleted project's deliverables go with its copy.
/// </remarks>
public sealed class DemandProjectDeliverableSyncHandler(
    IDemandManagementDbContext dbContext,
    IDispatcher dispatcher,
    ILogger<DemandProjectDeliverableSyncHandler> logger)
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ILogger<DemandProjectDeliverableSyncHandler> _logger = logger;

    /// <summary>Builds the new deliverable's copy from its source, unless one already exists.</summary>
    public async Task Handle(ProjectDeliverableAddedEvent @event, CancellationToken cancellationToken)
    {
        if (await _dbContext.DemandProjectDeliverables.AnyAsync(x => x.Id == @event.DeliverableId, cancellationToken))
        {
            _logger.LogInformation("Demand Management {SystemActionType} for a new Project Deliverable skipped: Deliverable {DeliverableId} already has a copy.", SystemActionType.ServiceDataReplication, @event.DeliverableId);
            return;
        }

        await CreateFromSource(@event.DeliverableId, @event.Timestamp, cancellationToken);
    }

    /// <summary>Applies a rename; the copy keeps no other detail.</summary>
    public async Task Handle(ProjectDeliverableDetailsUpdatedEvent @event, CancellationToken cancellationToken)
    {
        await Apply(@event.DeliverableId, @event.Timestamp,
            d => d.ApplyName(@event.Details.Name, @event.Timestamp), "name", cancellationToken);
    }

    /// <summary>Applies a move to another status: what decides whether the requests it serves are met.</summary>
    public async Task Handle(ProjectDeliverableStatusChangedEvent @event, CancellationToken cancellationToken)
    {
        await Apply(@event.DeliverableId, @event.Timestamp,
            d => d.ApplyStatus(@event.ToCategory, @event.ToAlias, @event.Timestamp), "status", cancellationToken);
    }

    /// <summary>
    /// A workflow migration moves a deliverable's status without any <c>ProjectDeliverable*</c> event, so the
    /// engine's shared event is followed too, for deliverables only.
    /// </summary>
    public async Task Handle(StatusWorkflowSwitchedEvent @event, CancellationToken cancellationToken)
    {
        if (@event.OwnerType != ProjectDeliverableWorkflow.OwnerType)
            return;

        await Apply(@event.RecordId, @event.Timestamp,
            d => d.ApplyStatus(@event.ToCategory, (ProjectDeliverableStatusAlias)@event.ToAlias, @event.Timestamp),
            "workflow switch", cancellationToken);
    }

    /// <summary>Removes the deleted deliverable's copy; outcomes mapped to it keep its id.</summary>
    public async Task Handle(ProjectDeliverableRemovedEvent @event, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.DemandProjectDeliverables.FirstOrDefaultAsync(x => x.Id == @event.DeliverableId, cancellationToken);
        if (existing is null)
        {
            _logger.LogInformation("Demand Management {SystemActionType} for a removed Project Deliverable skipped: Deliverable {DeliverableId} has no copy.", SystemActionType.ServiceDataReplication, @event.DeliverableId);
            return;
        }

        _dbContext.DemandProjectDeliverables.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Demand Management {SystemActionType} for the Project Deliverable {DeliverableId} removed action.", SystemActionType.ServiceDataReplication, @event.DeliverableId);
    }

    private async Task Apply(Guid deliverableId, Instant timestamp, Func<DemandProjectDeliverable, bool> apply, string change, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.DemandProjectDeliverables.FirstOrDefaultAsync(x => x.Id == deliverableId, cancellationToken);
        if (existing is null)
        {
            await CreateFromSource(deliverableId, timestamp, cancellationToken);
            return;
        }

        if (!apply(existing))
        {
            _logger.LogInformation("Demand Management {SystemActionType} for a Project Deliverable {Change} skipped: Deliverable {DeliverableId} already holds this or a newer change.", SystemActionType.ServiceDataReplication, change, deliverableId);
            return;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Demand Management {SystemActionType} for the Project Deliverable {DeliverableId} {Change} change.", SystemActionType.ServiceDataReplication, deliverableId, change);
    }

    private async Task CreateFromSource(Guid deliverableId, Instant timestamp, CancellationToken cancellationToken)
    {
        var source = await _dispatcher.Send(new GetSimpleProjectDeliverableQuery(deliverableId), cancellationToken);
        if (source is null)
        {
            _logger.LogInformation("Demand Management {SystemActionType} copy not created: Project Deliverable {DeliverableId} no longer exists.", SystemActionType.ServiceDataReplication, deliverableId);
            return;
        }

        if (!await _dbContext.DemandProjects.AnyAsync(p => p.Id == source.ProjectId, cancellationToken))
        {
            var project = await _dispatcher.Send(new GetSimpleProjectWithLifecycleQuery(source.ProjectId), cancellationToken);
            if (project is null)
            {
                _logger.LogInformation("Demand Management {SystemActionType} copy not created: the Project {ProjectId} of Deliverable {DeliverableId} no longer exists.", SystemActionType.ServiceDataReplication, source.ProjectId, deliverableId);
                return;
            }

            await _dbContext.DemandProjects.AddAsync(new DemandProject(project, timestamp), cancellationToken);
        }

        await _dbContext.DemandProjectDeliverables.AddAsync(new DemandProjectDeliverable(source, timestamp), cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Demand Management {SystemActionType} creating Project Deliverable {DeliverableId} from its source.", SystemActionType.ServiceDataReplication, deliverableId);
    }
}
