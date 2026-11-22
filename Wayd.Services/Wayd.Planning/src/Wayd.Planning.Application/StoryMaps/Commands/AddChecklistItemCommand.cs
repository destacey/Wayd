using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record AddChecklistItemCommand(Guid StoryMapId, Guid TaskId, string Name) : ICommand<StoryMapTaskDto>, IRequireLinkedEmployee;

public sealed class AddChecklistItemCommandValidator : CustomValidator<AddChecklistItemCommand>
{
    public AddChecklistItemCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.TaskId).NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128);
    }
}

public sealed class AddChecklistItemCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier) : ICommandHandler<AddChecklistItemCommand, StoryMapTaskDto>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;

    public async Task<Result<StoryMapTaskDto>> Handle(AddChecklistItemCommand request, CancellationToken cancellationToken)
    {
        var map = await _planningDbContext.StoryMaps
            .IncludeAccess()
            .Include(m => m.Goals).ThenInclude(g => g.Steps).ThenInclude(s => s.Tasks)
            .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

        if (map is null)
            return Result.Failure<StoryMapTaskDto>("Story map not found.");

        var actor = await _access.ResolveActor(map, cancellationToken);

        var result = map.AddChecklistItem(request.TaskId, request.Name, actor);
        if (result.IsFailure)
            return Result.Failure<StoryMapTaskDto>(result.Error);

        await _planningDbContext.SaveChangesAsync(cancellationToken);

        var taskDto = map.Goals.SelectMany(g => g.Steps).SelectMany(s => s.Tasks)
            .First(t => t.Id == request.TaskId)
            .Adapt<StoryMapTaskDto>();

        await _notifier.NotifyTaskChecklistChanged(map.Id, taskDto);

        return Result.Success(taskDto);
    }
}
