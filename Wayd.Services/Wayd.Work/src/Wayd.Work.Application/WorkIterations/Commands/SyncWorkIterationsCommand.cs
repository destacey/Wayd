using Wayd.Common.Domain.Interfaces.Planning.Iterations;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkIterations.Commands;

public sealed record SyncWorkIterationsCommand(IEnumerable<ISimpleIteration> Iterations) : ICommand, ILongRunningRequest;

public sealed class SyncWorkIterationsCommandHandler(
    IWorkDbContext workDbContext,
    ILogger<SyncWorkIterationsCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<SyncWorkIterationsCommand>
{
    private const string AppRequestName = nameof(SyncWorkIterationsCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<SyncWorkIterationsCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(SyncWorkIterationsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.Iterations == null || !request.Iterations.Any())
            {
                _logger.LogInformation("No iterations to sync.");
                return Result.Success();
            }

            var now = _dateTimeProvider.Now;

            int createCount = 0;
            int updateCount = 0;
            int deleteCount = 0;

            var existingIterations = await _workDbContext.WorkIterations
                .ToListAsync(cancellationToken);
            var existingIds = existingIterations.Select(x => x.Id).ToHashSet();

            // Handle deletes
            var deleteIds = existingIds.Except(request.Iterations.Select(x => x.Id)).ToList();
            if (deleteIds.Count != 0)
            {
                var iterationsToDelete = existingIterations.Where(x => deleteIds.Contains(x.Id)).ToList();
                _workDbContext.WorkIterations.RemoveRange(iterationsToDelete);
                deleteCount = iterationsToDelete.Count;
            }

            // Handle creates and updates
            foreach (var iteration in request.Iterations)
            {
                var existingIteration = existingIterations.FirstOrDefault(x => x.Id == iteration.Id);
                if (existingIteration == null)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Creating new Work iteration {IterationId}.", iteration.Id);

                    var newIteration = new WorkIteration(iteration);

                    await _workDbContext.WorkIterations.AddAsync(newIteration, cancellationToken);
                    createCount++;
                }
                else
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Updating existing Work iteration {IterationId}.", iteration.Id);

                    var result = existingIteration.Update(iteration, now);
                    if (result.IsFailure)
                    {
                        _logger.LogWarning("Failed to update Work iteration {IterationId}: {ErrorMessage}.", iteration.Id, result.Error);
                    }
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
