using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Requests.Planning.Queries;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events.Planning.EstimationScales;
using Wayd.DemandManagement.Domain.Models.Estimation;

namespace Wayd.DemandManagement.Application.DemandEstimationScales.EventHandlers;

/// <summary>
/// Keeps Demand Management's <see cref="DemandEstimationScale"/> copy of each Planning estimation scale (same Id),
/// with its versions.
/// </summary>
/// <remarks>
/// The <see cref="IEstimationScaleEvent"/> events are delivered durably (see <c>DurableEventRoutes</c>): at least
/// once and in no guaranteed order. This handler follows the rules in "Consuming an event"
/// (docs/contributing/domain-events.mdx): a change older than the one the copy already holds is skipped, a
/// missing copy is created from the scale's current state, and a scale that no longer exists is left without a
/// copy. A deleted scale's copy goes, unless a desk pinned it or an agent estimated on it before the deletion reached
/// here; then it stays, inactive, for as long as anything uses it.
/// </remarks>
public sealed class DemandEstimationScaleSyncHandler(
    IDemandManagementDbContext dbContext,
    IDispatcher dispatcher,
    ILogger<DemandEstimationScaleSyncHandler> logger)
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ILogger<DemandEstimationScaleSyncHandler> _logger = logger;

    public async Task Handle(EstimationScaleCreatedEvent @event, CancellationToken cancellationToken)
    {
        if (await _dbContext.DemandEstimationScales.AnyAsync(x => x.Id == @event.Id, cancellationToken))
        {
            _logger.LogInformation("Demand Management {SystemActionType} for a new Estimation Scale skipped: Estimation Scale {EstimationScaleId} already has a copy.", SystemActionType.ServiceDataReplication, @event.Id);
            return;
        }

        await CreateFromSource(@event.Id, @event.Timestamp, cancellationToken);
    }

    public async Task Handle(EstimationScaleDetailsUpdatedEvent @event, CancellationToken cancellationToken)
    {
        await Apply(@event.Id, @event.Timestamp, s => s.ApplyName(@event.Name, @event.Timestamp), "name", cancellationToken);
    }

    public async Task Handle(EstimationScaleActivatedEvent @event, CancellationToken cancellationToken)
    {
        await Apply(@event.Id, @event.Timestamp, s => s.ApplyIsActive(true, @event.Timestamp), "activation", cancellationToken);
    }

    public async Task Handle(EstimationScaleDeactivatedEvent @event, CancellationToken cancellationToken)
    {
        await Apply(@event.Id, @event.Timestamp, s => s.ApplyIsActive(false, @event.Timestamp), "deactivation", cancellationToken);
    }

    public async Task Handle(EstimationScaleVersionPublishedEvent @event, CancellationToken cancellationToken)
    {
        await Apply(@event.Id, @event.Timestamp, s => s.AddVersion(@event.Version), "version", cancellationToken);
    }

    public async Task Handle(EstimationScaleDeletedEvent @event, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.DemandEstimationScales
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(x => x.Id == @event.Id, cancellationToken);
        if (existing is null)
        {
            _logger.LogInformation("Demand Management {SystemActionType} for a deleted Estimation Scale skipped: Estimation Scale {EstimationScaleId} has no copy.", SystemActionType.ServiceDataReplication, @event.Id);
            return;
        }

        // Planning checks the copies before deleting, but a desk can pin the scale or an agent estimate on it before
        // this event arrives, and their foreign keys would then fail every retry. The copy stays, inactive so nothing
        // chooses it again, until the scales sync finds nothing uses it. Planning never activates a deleted scale, so
        // every activation still in flight is older than this and is skipped.
        if (await _dbContext.IsInUse(existing.Id, cancellationToken))
        {
            if (!existing.ApplyIsActive(false, @event.Timestamp))
                return;

            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Demand Management {SystemActionType} kept the deleted Estimation Scale {EstimationScaleId}, inactive, because a desk or an estimate still uses it.", SystemActionType.ServiceDataReplication, @event.Id);
            return;
        }

        _dbContext.DemandEstimationScales.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Demand Management {SystemActionType} for the Estimation Scale {EstimationScaleId} deleted action.", SystemActionType.ServiceDataReplication, @event.Id);
    }

    private async Task Apply(Guid scaleId, Instant timestamp, Func<DemandEstimationScale, bool> apply, string change, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.DemandEstimationScales
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(x => x.Id == scaleId, cancellationToken);
        if (existing is null)
        {
            await CreateFromSource(scaleId, timestamp, cancellationToken);
            return;
        }

        if (!apply(existing))
        {
            _logger.LogInformation("Demand Management {SystemActionType} for an Estimation Scale {Change} skipped: Estimation Scale {EstimationScaleId} already holds this or a newer change.", SystemActionType.ServiceDataReplication, change, scaleId);
            return;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Demand Management {SystemActionType} for the Estimation Scale {EstimationScaleId} {Change} change.", SystemActionType.ServiceDataReplication, scaleId, change);
    }

    private async Task CreateFromSource(Guid scaleId, Instant timestamp, CancellationToken cancellationToken)
    {
        var sources = await _dispatcher.Send(new GetEstimationScaleSnapshotsQuery(scaleId), cancellationToken);
        if (sources.Count == 0)
        {
            _logger.LogInformation("Demand Management {SystemActionType} copy not created: Estimation Scale {EstimationScaleId} no longer exists.", SystemActionType.ServiceDataReplication, scaleId);
            return;
        }

        await _dbContext.DemandEstimationScales.AddAsync(new DemandEstimationScale(sources[0], timestamp), cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Demand Management {SystemActionType} creating Estimation Scale {EstimationScaleId} from its source.", SystemActionType.ServiceDataReplication, scaleId);
    }
}
