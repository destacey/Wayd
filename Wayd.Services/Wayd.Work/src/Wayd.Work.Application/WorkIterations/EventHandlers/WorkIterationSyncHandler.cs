using Wayd.Common.Application.Requests.Planning.Iterations;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkIterations.EventHandlers;

/// <summary>
/// Keeps the Work module's <c>WorkIteration</c> copy of each Planning iteration (same Id).
/// </summary>
/// <remarks>
/// The <c>Iteration*</c> events are delivered durably (see <c>DurableEventRoutes</c>): at least once and in no
/// guaranteed order, with a retry-with-cooldown → dead-letter failure policy. This handler follows the rules
/// in "Consuming an event" (docs/contributing/domain-events.mdx): a change older than the one the copy already
/// holds is skipped, a missing copy is created from the iteration's current state, and an iteration that no
/// longer exists is left without a copy.
/// </remarks>
public sealed class WorkIterationSyncHandler(IWorkDbContext workDbContext, IDispatcher dispatcher, ILogger<WorkIterationSyncHandler> logger)
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ILogger<WorkIterationSyncHandler> _logger = logger;

    public async Task Handle(IterationCreatedEvent @event, CancellationToken cancellationToken)
    {
        if (await _workDbContext.WorkIterations.AnyAsync(x => x.Id == @event.Id, cancellationToken))
        {
            _logger.LogInformation("Work {SystemActionType} for a new Iteration skipped: Iteration {IterationId} already has a copy.", SystemActionType.ServiceDataReplication, @event.Id);
            return;
        }

        await CreateFromSource(@event.Id, @event.Timestamp, cancellationToken);
    }

    public async Task Handle(IterationDetailsUpdatedEvent @event, CancellationToken cancellationToken)
    {
        await Apply(@event.Id, @event.Timestamp, i => i.ApplyDetails(@event.Name, @event.Type, EventActor.System, @event.Timestamp), "details", cancellationToken);
    }

    public async Task Handle(IterationDateRangeChangedEvent @event, CancellationToken cancellationToken)
    {
        await Apply(@event.Id, @event.Timestamp, i => i.ApplyDateRange(@event.DateRange, EventActor.System, @event.Timestamp), "date range", cancellationToken);
    }

    public async Task Handle(IterationStateChangedEvent @event, CancellationToken cancellationToken)
    {
        await Apply(@event.Id, @event.Timestamp, i => i.ApplyState(@event.ToState, EventActor.System, @event.Timestamp), "state", cancellationToken);
    }

    public async Task Handle(IterationTeamChangedEvent @event, CancellationToken cancellationToken)
    {
        await Apply(@event.Id, @event.Timestamp, i => i.ApplyTeam(@event.TeamId, EventActor.System, @event.Timestamp), "team", cancellationToken);
    }

    // Nothing raises the superseded type, but an envelope written as it before the switch can still be
    // waiting in the durable outbox; without this it would dead-letter rather than update the copy.
#pragma warning disable CS0618
    public async Task Handle(IterationUpdatedEvent @event, CancellationToken cancellationToken)
#pragma warning restore CS0618
    {
        await Apply(@event.Id, @event.Timestamp, i => i.ApplyRecord(@event, EventActor.System, @event.Timestamp), "record", cancellationToken);
    }

    public async Task Handle(IterationDeletedEvent @event, CancellationToken cancellationToken)
    {
        // TODO: work items are being updated via cascade delete, which may be undesirable. Consider changing this behavior. And then send events from those work items to update their dependencies.
        var iteration = await _workDbContext.WorkIterations.FirstOrDefaultAsync(x => x.Id == @event.Id, cancellationToken);
        if (iteration == null)
        {
            _logger.LogInformation("Work {SystemActionType} for a deleted Iteration skipped: Iteration {IterationId} has no copy.", SystemActionType.ServiceDataReplication, @event.Id);
            return;
        }

        _workDbContext.WorkIterations.Remove(iteration);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Work {SystemActionType} for the Iteration {IterationId} deleted action.", SystemActionType.ServiceDataReplication, @event.Id);
    }

    private async Task Apply(Guid iterationId, Instant timestamp, Func<WorkIteration, bool> apply, string change, CancellationToken cancellationToken)
    {
        var iteration = await _workDbContext.WorkIterations.FirstOrDefaultAsync(x => x.Id == iterationId, cancellationToken);
        if (iteration == null)
        {
            await CreateFromSource(iterationId, timestamp, cancellationToken);
            return;
        }

        if (!apply(iteration))
        {
            _logger.LogInformation("Work {SystemActionType} for an Iteration {Change} skipped: Iteration {IterationId} already holds this or a newer change.", SystemActionType.ServiceDataReplication, change, iterationId);
            return;
        }

        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Work {SystemActionType} for the Iteration {IterationId} {Change}.", SystemActionType.ServiceDataReplication, iterationId, change);
    }

    private async Task CreateFromSource(Guid iterationId, Instant timestamp, CancellationToken cancellationToken)
    {
        var source = await _dispatcher.Send(new GetSimpleIterationQuery(iterationId), cancellationToken);
        if (source is null)
        {
            _logger.LogInformation("Work {SystemActionType} copy not created: Iteration {IterationId} no longer exists.", SystemActionType.ServiceDataReplication, iterationId);
            return;
        }

        await _workDbContext.WorkIterations.AddAsync(new WorkIteration(source, timestamp), cancellationToken);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Work {SystemActionType} creating Iteration {IterationId} from its source.", SystemActionType.ServiceDataReplication, iterationId);
    }
}
