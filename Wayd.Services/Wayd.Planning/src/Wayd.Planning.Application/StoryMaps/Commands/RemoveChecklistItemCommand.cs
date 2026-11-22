using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record RemoveChecklistItemCommand(Guid StoryMapId, Guid TaskId, Guid ItemId) : ICommand, IRequireLinkedEmployee;

public sealed class RemoveChecklistItemCommandValidator : CustomValidator<RemoveChecklistItemCommand>
{
    public RemoveChecklistItemCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.TaskId).NotEmpty();
        RuleFor(c => c.ItemId).NotEmpty();
    }
}

public sealed class RemoveChecklistItemCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier) : ICommandHandler<RemoveChecklistItemCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;

    public async Task<Result> Handle(RemoveChecklistItemCommand request, CancellationToken cancellationToken)
    {
        var map = await _planningDbContext.StoryMaps
            .IncludeAccess()
            .Include(m => m.Goals).ThenInclude(g => g.Steps).ThenInclude(s => s.Tasks)
            .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

        if (map is null)
            return Result.Failure("Story map not found.");

        var actor = await _access.ResolveActor(map, cancellationToken);

        var result = map.RemoveChecklistItem(request.TaskId, request.ItemId, actor);
        if (result.IsFailure)
            return result;

        await _planningDbContext.SaveChangesAsync(cancellationToken);

        var taskDto = map.Goals.SelectMany(g => g.Steps).SelectMany(s => s.Tasks)
            .First(t => t.Id == request.TaskId)
            .Adapt<StoryMapTaskDto>();

        await _notifier.NotifyTaskChecklistChanged(map.Id, taskDto);

        return Result.Success();
    }
}
