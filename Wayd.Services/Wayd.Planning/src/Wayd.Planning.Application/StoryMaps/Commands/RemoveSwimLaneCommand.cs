using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record RemoveSwimLaneCommand(Guid StoryMapId, Guid SwimLaneId) : ICommand<int>;

public sealed class RemoveSwimLaneCommandValidator : CustomValidator<RemoveSwimLaneCommand>
{
    public RemoveSwimLaneCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.SwimLaneId).NotEmpty();
    }
}

public sealed class RemoveSwimLaneCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<RemoveSwimLaneCommandHandler> logger) : ICommandHandler<RemoveSwimLaneCommand, int>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<RemoveSwimLaneCommandHandler> _logger = logger;

    public async Task<Result<int>> Handle(RemoveSwimLaneCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .Include(m => m.Goals).ThenInclude(g => g.Steps).ThenInclude(s => s.Tasks)
                .Include(m => m.SwimLanes)
                .AsSplitQuery()
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure<int>("Story map not found.");

            var result = map.RemoveSwimLane(request.SwimLaneId);
            if (result.IsFailure)
                return Result.Failure<int>(result.Error);

            var movedCount = result.Value;

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            var defaultSwimLaneId = map.SwimLanes.Single(l => l.IsDefault).Id;
            await _notifier.NotifySwimLaneRemoved(map.Id, request.SwimLaneId, defaultSwimLaneId, movedCount);

            return Result.Success(movedCount);
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure<int>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
