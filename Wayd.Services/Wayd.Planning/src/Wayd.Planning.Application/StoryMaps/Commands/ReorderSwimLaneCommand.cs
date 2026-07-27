using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record ReorderSwimLaneCommand(Guid StoryMapId, Guid SwimLaneId, int NewOrder) : ICommand;

public sealed class ReorderSwimLaneCommandValidator : CustomValidator<ReorderSwimLaneCommand>
{
    public ReorderSwimLaneCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.SwimLaneId).NotEmpty();
    }
}

public sealed class ReorderSwimLaneCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<ReorderSwimLaneCommandHandler> logger) : ICommandHandler<ReorderSwimLaneCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<ReorderSwimLaneCommandHandler> _logger = logger;

    public async Task<Result> Handle(ReorderSwimLaneCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await StoryMapMutation.Apply(
                _planningDbContext,
                ct => _planningDbContext.StoryMaps
                    .Include(m => m.SwimLanes)
                    .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, ct),
                map => map.ReorderSwimLane(request.SwimLaneId, request.NewOrder)
                    .Map(() => map.SwimLanes.First(l => l.Id == request.SwimLaneId).Order),
                cancellationToken);

            if (result.IsFailure)
                return Result.Failure(result.Error);

            await _notifier.NotifySwimLaneReordered(request.StoryMapId, request.SwimLaneId, result.Value);

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
