namespace Wayd.Planning.Application.PlanningIntervals.Commands;

public sealed record UpdatePlanningIntervalObjectivesOrderCommand(Guid PlanningIntervalId, Dictionary<Guid, int?> Objectives) : ICommand;

public sealed class UpdatePlanningIntervalObjectivesOrderCommandValidator : CustomValidator<UpdatePlanningIntervalObjectivesOrderCommand>
{
    public UpdatePlanningIntervalObjectivesOrderCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(o => o.PlanningIntervalId)
            .NotEmpty()
            .WithMessage("A plan must be selected.");

        RuleFor(o => o.Objectives)
            .NotEmpty()
            .WithMessage("At least one objective must be provided.");
    }
}

public sealed class UpdatePlanningIntervalObjectivesOrderCommandHandler(IPlanningDbContext planningDbContext, ILogger<UpdatePlanningIntervalObjectivesOrderCommandHandler> logger) : ICommandHandler<UpdatePlanningIntervalObjectivesOrderCommand>
{
    private const string AppRequestName = nameof(UpdatePlanningIntervalObjectivesOrderCommand);

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ILogger<UpdatePlanningIntervalObjectivesOrderCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdatePlanningIntervalObjectivesOrderCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var objectiveIds = request.Objectives.Keys.ToList();

            var planningInterval = await _planningDbContext.PlanningIntervals
                .Include(p => p.Objectives.Where(o => objectiveIds.Contains(o.Id)))
                .FirstOrDefaultAsync(p => p.Id == request.PlanningIntervalId, cancellationToken);

            if (planningInterval is null)
            {
                _logger.LogWarning("Planning Interval {PlanningIntervalId} not found.", request.PlanningIntervalId);
                return Result.Failure($"Planning Interval {request.PlanningIntervalId} not found.");
            }

            var result = planningInterval.UpdateObjectivesOrder(request.Objectives);
            if (result.IsFailure)
            {
                _logger.LogWarning("Not all objectives provided were found. {Error}", result.Error);
                return Result.Failure("Not all objectives provided were found.");
            }

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
