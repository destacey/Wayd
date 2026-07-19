using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkIterations.EventHandlers;

/// <summary>
/// Replicates Planning <c>Iteration</c> changes into the Work domain's <c>WorkIteration</c> projection
/// (same Id).
/// </summary>
/// <remarks>
/// The <c>Iteration*</c> events are delivered durably (see <c>DurableEventRoutes</c>): enlisted in the
/// Wolverine outbox, delivered on a background thread, and governed by a retry-with-cooldown → dead-letter
/// failure policy. So this handler lets exceptions propagate — a transient DB failure is retried by Wolverine
/// and a poison message dead-letters. The per-message idempotency guards (create no-ops if the row already
/// exists; update/delete no-op if it does not) make redelivery safe.
/// </remarks>
public sealed class WorkIterationSyncHandler(IWorkDbContext workDbContext, ILogger<WorkIterationSyncHandler> logger, IDateTimeProvider dateTimeProvider)
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<WorkIterationSyncHandler> _logger = logger;

    // Retained as a constructor dependency to keep the handler's generated dispatch code (and thus the
    // committed Wolverine handler tree) stable; the update path takes its timestamp from the event.
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task Handle(IterationCreatedEvent @event, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Handling Work {SystemActionType} for a new Iteration {IterationId}.", SystemActionType.ServiceDataReplication, @event.Id);

        await CreateIteration(@event, cancellationToken);
    }

    public async Task Handle(IterationUpdatedEvent @event, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Handling Work {SystemActionType} for an updated Iteration {IterationId}.", SystemActionType.ServiceDataReplication, @event.Id);

        await UpdateIteration(@event, cancellationToken);
    }

    public async Task Handle(IterationDeletedEvent @event, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Handling Work {SystemActionType} for a deleted Iteration {IterationId}.", SystemActionType.ServiceDataReplication, @event.Id);

        await DeleteIteration(@event, cancellationToken);
    }

    private async Task CreateIteration(IterationCreatedEvent createdEvent, CancellationToken cancellationToken)
    {
        // Idempotency guard, not error handling: a redelivery or a race with the Hangfire SyncWorkIterations
        // bulk path may find the projection already present — that is a success, not a fault.
        if (await _workDbContext.WorkIterations.AnyAsync(x => x.Id == createdEvent.Id, cancellationToken))
        {
            _logger.LogInformation("Work {SystemActionType} for a new Iteration skipped: Iteration {IterationId} already exists in the Work system.", SystemActionType.ServiceDataReplication, createdEvent.Id);
            return;
        }

        var iteration = new WorkIteration(createdEvent);

        await _workDbContext.WorkIterations.AddAsync(iteration, cancellationToken);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Work {SystemActionType} for the Iteration {IterationId} created action.", SystemActionType.ServiceDataReplication, createdEvent.Id);
    }

    private async Task UpdateIteration(IterationUpdatedEvent updatedEvent, CancellationToken cancellationToken)
    {
        var iteration = await _workDbContext.WorkIterations.FirstOrDefaultAsync(x => x.Id == updatedEvent.Id, cancellationToken);
        if (iteration == null)
        {
            // The create event has not been applied yet (out-of-order delivery) or the iteration was deleted.
            // No-op rather than throw: a create redelivery or the Hangfire bulk sync will converge the state.
            _logger.LogWarning("Work {SystemActionType} for an updated Iteration skipped: Iteration {IterationId} does not exist in the Work system.", SystemActionType.ServiceDataReplication, updatedEvent.Id);
            return;
        }

        var result = iteration.Update(updatedEvent, updatedEvent.Timestamp);
        if (result.IsFailure)
        {
            // We loaded the iteration by this exact Id, so Update cannot legitimately reject it — a failure
            // here means a real invariant break, not a transient fault. Throw so the durable failure policy
            // retries and then dead-letters it for inspection, rather than silently ACKing and leaving the
            // Work projection stale.
            throw new InvalidOperationException(
                $"WorkIteration {updatedEvent.Id} rejected a replication update: {result.Error}");
        }

        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Work {SystemActionType} for the Iteration {IterationId} updated action.", SystemActionType.ServiceDataReplication, updatedEvent.Id);
    }

    private async Task DeleteIteration(IterationDeletedEvent deletedEvent, CancellationToken cancellationToken)
    {
        // TODO: work items are being updated via cascade delete, which may be undesirable. Consider changing this behavior. And then send events from those work items to update their dependencies.
        var iteration = await _workDbContext.WorkIterations.FirstOrDefaultAsync(x => x.Id == deletedEvent.Id, cancellationToken);
        if (iteration == null)
        {
            // Already gone (redelivery, or the iteration never replicated) — deleting nothing is the goal state.
            _logger.LogInformation("Work {SystemActionType} for a deleted Iteration skipped: Iteration {IterationId} does not exist in the Work system.", SystemActionType.ServiceDataReplication, deletedEvent.Id);
            return;
        }

        _workDbContext.WorkIterations.Remove(iteration);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Work {SystemActionType} for the Iteration {IterationId} deleted action.", SystemActionType.ServiceDataReplication, deletedEvent.Id);
    }
}
