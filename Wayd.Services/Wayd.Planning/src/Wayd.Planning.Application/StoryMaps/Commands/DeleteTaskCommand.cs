using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record DeleteTaskCommand(Guid StoryMapId, Guid TaskId) : ICommand, IRequireLinkedEmployee;

public sealed class DeleteTaskCommandValidator : CustomValidator<DeleteTaskCommand>
{
    public DeleteTaskCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.TaskId).NotEmpty();
    }
}

public sealed class DeleteTaskCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier) : ICommandHandler<DeleteTaskCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;

    public async Task<Result> Handle(DeleteTaskCommand request, CancellationToken cancellationToken)
    {
        var map = await _planningDbContext.StoryMaps
            .IncludeAccess()
            .Include(m => m.Goals).ThenInclude(g => g.Steps).ThenInclude(s => s.Tasks)
            .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

        if (map is null)
            return Result.Failure("Story map not found.");

        var actor = await _access.ResolveActor(map, cancellationToken);

        var result = map.DeleteTask(request.TaskId, actor);
        if (result.IsFailure)
            return result;

        await _planningDbContext.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyTaskDeleted(map.Id, request.TaskId);

        return Result.Success();
    }
}
