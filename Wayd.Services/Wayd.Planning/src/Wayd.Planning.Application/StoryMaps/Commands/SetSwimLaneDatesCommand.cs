using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record SetSwimLaneDatesCommand(Guid StoryMapId, Guid SwimLaneId, LocalDate? StartDate, LocalDate? EndDate) : ICommand, IRequireLinkedEmployee;

public sealed class SetSwimLaneDatesCommandValidator : CustomValidator<SetSwimLaneDatesCommand>
{
    public SetSwimLaneDatesCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.SwimLaneId).NotEmpty();
    }
}

public sealed class SetSwimLaneDatesCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier) : ICommandHandler<SetSwimLaneDatesCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;

    public async Task<Result> Handle(SetSwimLaneDatesCommand request, CancellationToken cancellationToken)
    {
        var map = await _planningDbContext.StoryMaps
            .IncludeAccess()
            .Include(m => m.SwimLanes)
            .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

        if (map is null)
            return Result.Failure("Story map not found.");

        var actor = await _access.ResolveActor(map, cancellationToken);

        var result = map.SetSwimLaneDates(request.SwimLaneId, request.StartDate, request.EndDate, actor);
        if (result.IsFailure)
            return result;

        await _planningDbContext.SaveChangesAsync(cancellationToken);

        var laneDto = map.SwimLanes.First(l => l.Id == request.SwimLaneId).Adapt<StoryMapSwimLaneDto>();
        await _notifier.NotifySwimLaneDatesChanged(map.Id, laneDto);

        return Result.Success();
    }
}
