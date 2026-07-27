using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record MoveTaskCommand(Guid StoryMapId, Guid TaskId, Guid TargetStepId, Guid TargetSwimLaneId, int NewOrder) : ICommand;

public sealed class MoveTaskCommandValidator : CustomValidator<MoveTaskCommand>
{
    public MoveTaskCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.TaskId).NotEmpty();
        RuleFor(c => c.TargetStepId).NotEmpty();
        RuleFor(c => c.TargetSwimLaneId).NotEmpty();
        RuleFor(c => c.NewOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class MoveTaskCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<MoveTaskCommandHandler> logger) : ICommandHandler<MoveTaskCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<MoveTaskCommandHandler> _logger = logger;

    public async Task<Result> Handle(MoveTaskCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await StoryMapMutation.Apply(
                _planningDbContext,
                ct => _planningDbContext.StoryMaps
                    .Include(m => m.Goals).ThenInclude(g => g.Steps).ThenInclude(s => s.Tasks)
                    .Include(m => m.SwimLanes)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, ct),
                map => map.MoveTask(request.TaskId, request.TargetStepId, request.TargetSwimLaneId, request.NewOrder)
                    .Map(() => map.Goals
                        .SelectMany(g => g.Steps)
                        .SelectMany(s => s.Tasks)
                        .First(t => t.Id == request.TaskId)
                        .Order),
                cancellationToken);

            if (result.IsFailure)
                return Result.Failure(result.Error);

            await _notifier.NotifyTaskMoved(request.StoryMapId, request.TaskId, request.TargetStepId, request.TargetSwimLaneId, result.Value);

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
