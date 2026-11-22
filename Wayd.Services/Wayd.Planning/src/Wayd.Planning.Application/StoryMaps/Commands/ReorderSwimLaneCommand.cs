using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record ReorderSwimLaneCommand(Guid StoryMapId, Guid SwimLaneId, int NewOrder) : ICommand, IRequireLinkedEmployee;

public sealed class ReorderSwimLaneCommandValidator : CustomValidator<ReorderSwimLaneCommand>
{
    public ReorderSwimLaneCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.SwimLaneId).NotEmpty();
    }
}

public sealed class ReorderSwimLaneCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier) : ICommandHandler<ReorderSwimLaneCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;

    public async Task<Result> Handle(ReorderSwimLaneCommand request, CancellationToken cancellationToken)
    {
        var result = await StoryMapMutation.Apply(
            _planningDbContext,
            _access,
            ct => _planningDbContext.StoryMaps
                .IncludeAccess()
                .Include(m => m.SwimLanes)
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, ct),
            (map, actor) => map.ReorderSwimLane(request.SwimLaneId, request.NewOrder, actor)
                .Map(() => map.SwimLanes.First(l => l.Id == request.SwimLaneId).Order),
            cancellationToken);

        if (result.IsFailure)
            return Result.Failure(result.Error);

        await _notifier.NotifySwimLaneReordered(request.StoryMapId, request.SwimLaneId, result.Value);

        return Result.Success();
    }
}
