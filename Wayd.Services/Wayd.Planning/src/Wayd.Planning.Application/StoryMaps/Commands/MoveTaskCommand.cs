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
            var map = await _planningDbContext.StoryMaps
                .Include(m => m.Goals).ThenInclude(g => g.Steps).ThenInclude(s => s.Tasks)
                .Include(m => m.SwimLanes)
                .Include(m => m.Personas)
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure("Story map not found.");

            var result = map.MoveTask(request.TaskId, request.TargetStepId, request.TargetSwimLaneId, request.NewOrder);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);
            await _notifier.NotifyTaskMoved(map.Id, request.TaskId, request.TargetStepId, request.TargetSwimLaneId, request.NewOrder);

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
