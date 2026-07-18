using Wayd.Common.Application.Requests.Goals.Commands;
using Wayd.Goals.Application.Persistence;

namespace Wayd.Goals.Application.Objectives.Commands;

public sealed class UpdateObjectivesOrderCommandHandler(IGoalsDbContext goalsDbContext, ILogger<UpdateObjectivesOrderCommandHandler> logger) : ICommandHandler<UpdateObjectivesOrderCommand>
{
    private const string AppRequestName = nameof(UpdateObjectivesOrderCommand);

    private readonly IGoalsDbContext _goalsDbContext = goalsDbContext;
    private readonly ILogger<UpdateObjectivesOrderCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateObjectivesOrderCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var objectives = await _goalsDbContext.Objectives
                .Where(o => request.Objectives.Keys.Contains(o.Id))
                .ToListAsync(cancellationToken);

            if (objectives is null)
            {
                _logger.LogWarning("Objectives not found.");
                return Result.Failure("Objectives not found.");
            }

            if (objectives.Count != request.Objectives.Count)
            {
                var missingObjectives = request.Objectives.Keys.Except(objectives.Select(o => o.Id));
                _logger.LogWarning("Not all objectives provided were found. The following objectives were not found: {ObjectiveIds}", missingObjectives);
                return Result.Failure("Not all objectives provided were found.");
            }

            foreach (var objective in objectives)
            {
                objective.UpdateOrder(request.Objectives[objective.Id]);
            }

            await _goalsDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
