using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record ReorderGoalCommand(Guid StoryMapId, Guid GoalId, int NewOrder) : ICommand, IRequireLinkedEmployee;

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

public sealed class ReorderGoalCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier, ILogger<ReorderGoalCommandHandler> logger) : ICommandHandler<ReorderGoalCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<ReorderGoalCommandHandler> _logger = logger;

    public async Task<Result> Handle(ReorderGoalCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await StoryMapMutation.Apply(
                _planningDbContext,
                _access,
                ct => _planningDbContext.StoryMaps
                    .IncludeAccess()
                    .Include(m => m.Goals)
                    .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, ct),
                (map, actor) => map.ReorderGoal(request.GoalId, request.NewOrder, actor)
                    .Map(() => map.Goals.First(g => g.Id == request.GoalId).Order),
                cancellationToken);

            if (result.IsFailure)
                return Result.Failure(result.Error);

            await _notifier.NotifyGoalReordered(request.StoryMapId, request.GoalId, result.Value);

            return Result.Success();
        }
        catch (Exception ex) when (ex is not ForbiddenException)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name}", requestName);
            return Result.Failure($"Wayd Request: Exception for Request {requestName}");
        }
    }
}
