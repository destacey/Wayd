using Wayd.Common.Application.Requests.Goals.Commands;

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

public sealed class UpdatePlanningIntervalObjectivesOrderCommandHandler(IPlanningDbContext planningDbContext, IDispatcher dispatcher, ILogger<UpdatePlanningIntervalObjectivesOrderCommandHandler> logger) : ICommandHandler<UpdatePlanningIntervalObjectivesOrderCommand>
{
    private const string AppRequestName = nameof(UpdatePlanningIntervalObjectivesOrderCommand);

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ILogger<UpdatePlanningIntervalObjectivesOrderCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdatePlanningIntervalObjectivesOrderCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var piObjectives = await _planningDbContext.PlanningIntervals
                .Where(p => p.Id == request.PlanningIntervalId)
                .SelectMany(p => p.Objectives
                    .Where(o => request.Objectives.Keys.Contains(o.Id))
                    .Select(o => new { o.Id, o.ObjectiveId }))
                .ToListAsync(cancellationToken);

            if (piObjectives is null)
            {
                _logger.LogWarning("Planning Interval {PlanningIntervalId} not found.", request.PlanningIntervalId);
                return Result.Failure<int>($"Planning Interval {request.PlanningIntervalId} not found.");
            }

            if (piObjectives.Count != request.Objectives.Count)
            {
                var missingObjectives = request.Objectives.Keys.Except(piObjectives.Select(o => o.Id));
                _logger.LogWarning("Not all objectives provided were found. The following objectives were not found: {PlanningIntervalObjectiveIds}", missingObjectives);
                return Result.Failure("Not all objectives provided were found.");
            }

            // map the PI objectives values to the Goal objectives values
            Dictionary<Guid, int?> updatedGoalObjectives = [];
            foreach (var piObjective in piObjectives)
            {
                updatedGoalObjectives.Add(piObjective.ObjectiveId, request.Objectives[piObjective.Id]);
            }

            var objectivResult = await _dispatcher.Send(new UpdateObjectivesOrderCommand(updatedGoalObjectives), cancellationToken);

            return objectivResult.IsSuccess
                ? Result.Success()
                : Result.Failure(objectivResult.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
