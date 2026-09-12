namespace Wayd.Planning.Application.PlanningIntervals.Commands;

public sealed record DeletePlanningIntervalObjectiveCommand(Guid PlanningIntervalId, Guid PlanningIntervalObjectiveId) : ICommand;

public sealed class DeletePlanningIntervalObjectiveCommandHandler(IPlanningDbContext planningDbContext, ILogger<DeletePlanningIntervalObjectiveCommandHandler> logger) : ICommandHandler<DeletePlanningIntervalObjectiveCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ILogger<DeletePlanningIntervalObjectiveCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeletePlanningIntervalObjectiveCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var planningInterval = await _planningDbContext.PlanningIntervals
                .Include(pi => pi.Objectives.Where(o => o.Id == request.PlanningIntervalObjectiveId))
                .FirstOrDefaultAsync(p => p.Id == request.PlanningIntervalId, cancellationToken);
            if (planningInterval is null)
            {
                _logger.LogError("Planning Interval {PlanningIntervalId} not found.", request.PlanningIntervalId);
                return Result.Failure($"Planning Interval {request.PlanningIntervalId} not found.");
            }

            var piObjective = planningInterval.Objectives.FirstOrDefault(o => o.Id == request.PlanningIntervalObjectiveId);
            if (piObjective is null)
            {
                _logger.LogError("Planning Interval Objective {PlanningIntervalObjectiveId} not found.", request.PlanningIntervalObjectiveId);
                return Result.Failure($"Planning Interval Objective {request.PlanningIntervalObjectiveId} not found.");
            }

            var deleteResult = planningInterval.DeleteObjective(request.PlanningIntervalObjectiveId);
            if (deleteResult.IsFailure)
            {
                _logger.LogError("Unable to delete Planning Interval Objective {PlanningIntervalObjectiveId} from Planning Interval {PlanningIntervalId}.  Error: {Error}", request.PlanningIntervalObjectiveId, request.PlanningIntervalId, deleteResult.Error);
                return Result.Failure($"Unable to remove Planning Interval Objective {request.PlanningIntervalObjectiveId} from Planning Interval {request.PlanningIntervalId}. Error: {deleteResult.Error}");
            }

            // TODO: this is a hack to ensure the PI objective is soft deleted.  We should be able to just remove it from the collection and save changes.
            _planningDbContext.Entry(piObjective).State = EntityState.Deleted;
            await _planningDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Planning Interval Objective {PlanningIntervalObjectiveId} from Planning Interval {PlanningIntervalId}.", request.PlanningIntervalObjectiveId, request.PlanningIntervalId);
            return Result.Failure($"Error deleting Planning Interval Objective {request.PlanningIntervalObjectiveId} from Planning Interval {request.PlanningIntervalId}.");
        }
    }
}
