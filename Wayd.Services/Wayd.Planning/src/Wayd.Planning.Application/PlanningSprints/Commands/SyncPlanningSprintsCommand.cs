using Wayd.Common.Domain.Interfaces.Planning.Iterations;

namespace Wayd.Planning.Application.PlanningSprints.Commands;

/// <param name="Sprints">Every sprint, as read from the source.</param>
/// <param name="AsOf">When the source was read, taken before the read began.</param>
public sealed record SyncPlanningSprintsCommand(IEnumerable<ISimpleIteration> Sprints, Instant AsOf) : ICommand, ILongRunningRequest;

public sealed class SyncPlanningSprintsCommandHandler(
    IPlanningDbContext planningDbContext,
    ILogger<SyncPlanningSprintsCommandHandler> logger)
    : ICommandHandler<SyncPlanningSprintsCommand>
{
    private const string AppRequestName = nameof(SyncPlanningSprintsCommand);

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ILogger<SyncPlanningSprintsCommandHandler> _logger = logger;

    public async Task<Result> Handle(SyncPlanningSprintsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.Sprints == null || !request.Sprints.Any())
            {
                _logger.LogInformation("No sprints to sync.");
                return Result.Success();
            }

            int createCount = 0;
            int updateCount = 0;
            int deleteCount = 0;

            var existingSprints = await _planningDbContext.PlanningSprints
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            var sourceIds = request.Sprints.Select(x => x.Id).ToHashSet();

            // A copy that took a change after the read belongs to a sprint created after it, not a deleted one.
            // A mapped copy stays: only its deletion event unmaps it, through the PI that records the change.
            var mappedSprintIds = await _planningDbContext.PlanningIntervalIterationSprints
                .Select(s => s.SprintId)
                .ToHashSetAsync(cancellationToken);
            var sprintsToDelete = existingSprints.Values
                .Where(x => !sourceIds.Contains(x.Id) && !x.Watermarks.AnyAfter(request.AsOf) && !mappedSprintIds.Contains(x.Id))
                .ToList();
            if (sprintsToDelete.Count != 0)
            {
                _planningDbContext.PlanningSprints.RemoveRange(sprintsToDelete);
                deleteCount = sprintsToDelete.Count;
            }

            foreach (var sprint in request.Sprints)
            {
                if (!existingSprints.TryGetValue(sprint.Id, out var existingSprint))
                {
                    await _planningDbContext.PlanningSprints.AddAsync(new PlanningSprint(sprint, request.AsOf), cancellationToken);
                    createCount++;
                }
                else if (existingSprint.Resync(sprint, request.AsOf))
                {
                    updateCount++;
                }
            }

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Sync Planning sprints completed. Created: {CreateCount}, Updated: {UpdateCount}, Deleted: {DeleteCount}.",
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
