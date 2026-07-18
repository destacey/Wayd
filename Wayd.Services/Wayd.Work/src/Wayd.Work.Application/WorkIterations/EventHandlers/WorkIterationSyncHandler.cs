using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkIterations.EventHandlers;

public sealed class WorkIterationSyncHandler(IWorkDbContext workDbContext, ILogger<WorkIterationSyncHandler> logger, IDateTimeProvider dateTimeProvider)
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<WorkIterationSyncHandler> _logger = logger;
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
        try
        {
            if (await _workDbContext.WorkIterations.AnyAsync(x => x.Id == createdEvent.Id, cancellationToken))
            {
                _logger.LogCritical("Error processing Work {SystemActionType} for a new Iteration. Iteration {IterationId} already exists in the Work system.", SystemActionType.ServiceDataReplication, createdEvent.Id);
                return;
            }

            var iteration = new WorkIteration(createdEvent);

            await _workDbContext.WorkIterations.AddAsync(iteration, cancellationToken);
            await _workDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successful Work {SystemActionType} for the Iteration {IterationId} created action.", SystemActionType.ServiceDataReplication, createdEvent.Id);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Exception handling Work {SystemActionType} for the Iteration {IterationId} created action.", SystemActionType.ServiceDataReplication, createdEvent.Id);
        }
    }

    private async Task UpdateIteration(IterationUpdatedEvent updatedEvent, CancellationToken cancellationToken)
    {
        try
        {
            var iteration = await _workDbContext.WorkIterations.FirstOrDefaultAsync(x => x.Id == updatedEvent.Id, cancellationToken);
            if (iteration == null)
            {
                _logger.LogCritical("Error processing Work {SystemActionType} for an updated Iteration. Iteration {IterationId} does not exist in the Work system.", SystemActionType.ServiceDataReplication, updatedEvent.Id);
                return;
            }

            var result = iteration.Update(updatedEvent, updatedEvent.Timestamp);
            if (result.IsFailure)
            {
                _logger.LogCritical("Error processing Work {SystemActionType} for an updated Iteration. Iteration {IterationId} update failed with error: {Error}.", SystemActionType.ServiceDataReplication, updatedEvent.Id, result.Error);
                return;
            }

            await _workDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successful Work {SystemActionType} for the Iteration {IterationId} updated action.", SystemActionType.ServiceDataReplication, updatedEvent.Id);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Exception handling Work {SystemActionType} for the Iteration {IterationId} updated action.", SystemActionType.ServiceDataReplication, updatedEvent.Id);
        }
    }

    private async Task DeleteIteration(IterationDeletedEvent deletedEvent, CancellationToken cancellationToken)
    {
        // TODO: work items are being updated via cascade delete, which may be undesirable. Consider changing this behavior. And then send events from those work items to update their dependencies.
        try
        {
            var iteration = await _workDbContext.WorkIterations.FirstOrDefaultAsync(x => x.Id == deletedEvent.Id, cancellationToken);
            if (iteration == null)
            {
                _logger.LogCritical("Error processing Work {SystemActionType} for a deleted Iteration. Iteration {IterationId} does not exist in the Work system.", SystemActionType.ServiceDataReplication, deletedEvent.Id);
                return;
            }

            _workDbContext.WorkIterations.Remove(iteration);

            await _workDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successful Work {SystemActionType} for the Iteration {IterationId} deleted action.", SystemActionType.ServiceDataReplication, deletedEvent.Id);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Exception handling Work {SystemActionType} for the Iteration {IterationId} deleted action.", SystemActionType.ServiceDataReplication, deletedEvent.Id);
        }
    }
}
