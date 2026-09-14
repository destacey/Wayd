namespace Wayd.Planning.Application.PlanningIntervals.HealthChecks.Commands;

public sealed record DeletePlanningIntervalObjectiveHealthCheckCommand(
    Guid PlanningIntervalObjectiveId,
    Guid HealthCheckId) : ICommand;

public sealed class DeletePlanningIntervalObjectiveHealthCheckCommandValidator
    : CustomValidator<DeletePlanningIntervalObjectiveHealthCheckCommand>
{
    public DeletePlanningIntervalObjectiveHealthCheckCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.PlanningIntervalObjectiveId)
            .NotEmpty();

        RuleFor(c => c.HealthCheckId)
            .NotEmpty();
    }
}

public sealed class DeletePlanningIntervalObjectiveHealthCheckCommandHandler(
    IPlanningDbContext planningDbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<DeletePlanningIntervalObjectiveHealthCheckCommandHandler> logger)
    : ICommandHandler<DeletePlanningIntervalObjectiveHealthCheckCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<DeletePlanningIntervalObjectiveHealthCheckCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeletePlanningIntervalObjectiveHealthCheckCommand request, CancellationToken cancellationToken)
    {
        var objective = await _planningDbContext.PlanningIntervalObjectives
            .Include(o => o.HealthChecks)
            .FirstOrDefaultAsync(o => o.Id == request.PlanningIntervalObjectiveId, cancellationToken);

        if (objective is null)
        {
            _logger.LogWarning("Planning Interval Objective {ObjectiveId} not found.", request.PlanningIntervalObjectiveId);
            return Result.Failure($"Planning Interval Objective {request.PlanningIntervalObjectiveId} not found.");
        }

        var removeResult = objective.RemoveHealthCheck(request.HealthCheckId,
            EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()), _dateTimeProvider.Now);
        if (removeResult.IsFailure)
        {
            _logger.LogError("Unable to remove health check {HealthCheckId} from objective {ObjectiveId}.  Error: {Error}", request.HealthCheckId, request.PlanningIntervalObjectiveId, removeResult.Error);
            return Result.Failure(removeResult.Error);
        }

        await _planningDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
