using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record AddTaskCommand(Guid StoryMapId, Guid StepId, string Title, Guid? SwimLaneId) : ICommand<StoryMapTaskDto>, IRequireLinkedEmployee;

public sealed class AddTaskCommandValidator : CustomValidator<AddTaskCommand>
{
    public AddTaskCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.StepId).NotEmpty();

        RuleFor(c => c.Title)
            .NotEmpty()
            .MaximumLength(128);
    }
}

public sealed class AddTaskCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier) : ICommandHandler<AddTaskCommand, StoryMapTaskDto>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;

    public async Task<Result<StoryMapTaskDto>> Handle(AddTaskCommand request, CancellationToken cancellationToken)
    {
        var map = await _planningDbContext.StoryMaps
            .IncludeAccess()
            .Include(m => m.Goals).ThenInclude(g => g.Steps).ThenInclude(s => s.Tasks)
            .Include(m => m.SwimLanes)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

        if (map is null)
            return Result.Failure<StoryMapTaskDto>("Story map not found.");

        var actor = await _access.ResolveActor(map, cancellationToken);

        var result = map.AddTask(request.StepId, request.Title, request.SwimLaneId, actor);
        if (result.IsFailure)
            return Result.Failure<StoryMapTaskDto>(result.Error);

        await _planningDbContext.SaveChangesAsync(cancellationToken);

        var dto = result.Value.Adapt<StoryMapTaskDto>();
        await _notifier.NotifyTaskAdded(map.Id, dto);

        return Result.Success(dto);
    }
}
