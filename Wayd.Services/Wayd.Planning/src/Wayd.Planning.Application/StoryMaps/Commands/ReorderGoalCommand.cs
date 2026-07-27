using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record ReorderGoalCommand(Guid StoryMapId, Guid GoalId, int NewOrder) : ICommand;

public sealed class ReorderGoalCommandValidator : CustomValidator<ReorderGoalCommand>
{
    public ReorderGoalCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.GoalId).NotEmpty();
        RuleFor(c => c.NewOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class ReorderGoalCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<ReorderGoalCommandHandler> logger) : ICommandHandler<ReorderGoalCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<ReorderGoalCommandHandler> _logger = logger;

    public async Task<Result> Handle(ReorderGoalCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await StoryMapMutation.Apply(
                _planningDbContext,
                ct => _planningDbContext.StoryMaps
                    .Include(m => m.Goals)
                    .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, ct),
                map => map.ReorderGoal(request.GoalId, request.NewOrder)
                    .Map(() => map.Goals.First(g => g.Id == request.GoalId).Order),
                cancellationToken);

            if (result.IsFailure)
                return Result.Failure(result.Error);

            await _notifier.NotifyGoalReordered(request.StoryMapId, request.GoalId, result.Value);

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
