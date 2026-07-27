using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record MoveStepCommand(Guid StoryMapId, Guid StepId, Guid TargetGoalId, int NewOrder) : ICommand;

public sealed class MoveStepCommandValidator : CustomValidator<MoveStepCommand>
{
    public MoveStepCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();

        RuleFor(c => c.StepId).NotEmpty();

        RuleFor(c => c.TargetGoalId).NotEmpty();

        RuleFor(c => c.NewOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class MoveStepCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<MoveStepCommandHandler> logger) : ICommandHandler<MoveStepCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<MoveStepCommandHandler> _logger = logger;

    public async Task<Result> Handle(MoveStepCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await StoryMapMutation.Apply(
                _planningDbContext,
                ct => _planningDbContext.StoryMaps
                    .Include(m => m.Goals).ThenInclude(g => g.Steps)
                    .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, ct),
                map => map.MoveStep(request.StepId, request.TargetGoalId, request.NewOrder)
                    .Map(() => map.Goals
                        .SelectMany(g => g.Steps)
                        .First(s => s.Id == request.StepId)
                        .Order),
                cancellationToken);

            if (result.IsFailure)
                return Result.Failure(result.Error);

            await _notifier.NotifyStepMoved(request.StoryMapId, request.StepId, request.TargetGoalId, result.Value);

            return Result.Success();
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
