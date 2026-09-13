using Wayd.Common.Domain.Interfaces.Planning.Iterations;
using Wayd.Work.Application.Persistence;
using Wayd.Common.Domain.Events;

namespace Wayd.Work.Application.WorkIterations.Commands;

/// <param name="Iterations">Every Planning iteration, as read from the source.</param>
/// <param name="AsOf">When the source was read, taken before the read began.</param>
public sealed record SyncWorkIterationsCommand(IEnumerable<ISimpleIteration> Iterations, Instant AsOf) : ICommand, ILongRunningRequest;

public sealed class SyncWorkIterationsCommandHandler(
    IWorkDbContext workDbContext,
    ILogger<SyncWorkIterationsCommandHandler> logger)
    : ICommandHandler<SyncWorkIterationsCommand>
{
    private const string AppRequestName = nameof(SyncWorkIterationsCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<SyncWorkIterationsCommandHandler> _logger = logger;

    public async Task<Result> Handle(SyncWorkIterationsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.Iterations == null || !request.Iterations.Any())
            {
                _logger.LogInformation("No iterations to sync.");
                return Result.Success();
            }

            int createCount = 0;
            int updateCount = 0;
            int deleteCount = 0;

            var existingIterations = await _workDbContext.WorkIterations
                .ToListAsync(cancellationToken);

            var sourceIds = request.Iterations.Select(x => x.Id).ToHashSet();

            // A copy that took a change after the read belongs to an iteration created after it, not a deleted one.
            var iterationsToDelete = existingIterations
                .Where(x => !sourceIds.Contains(x.Id) && !x.Watermarks.AnyAfter(request.AsOf))
                .ToList();
            if (iterationsToDelete.Count != 0)
            {
                _workDbContext.WorkIterations.RemoveRange(iterationsToDelete);
                deleteCount = iterationsToDelete.Count;
            }

            foreach (var iteration in request.Iterations)
            {
                var existingIteration = existingIterations.FirstOrDefault(x => x.Id == iteration.Id);
                if (existingIteration == null)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Creating new Work iteration {IterationId}.", iteration.Id);

                    await _workDbContext.WorkIterations.AddAsync(new WorkIteration(iteration, request.AsOf), cancellationToken);
                    createCount++;
                }
                else if (existingIteration.Resync(iteration, EventActor.System, request.AsOf))
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Updated existing Work iteration {IterationId}.", iteration.Id);

                    updateCount++;
                }
            }

            await _workDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Sync Work iterations completed. Created: {CreateCount}, Updated: {UpdateCount}, Deleted: {DeleteCount}.",
                createCount, updateCount, deleteCount);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command.", AppRequestName);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
