using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record RenameChecklistItemCommand(Guid StoryMapId, Guid TaskId, Guid ItemId, string Name) : ICommand, IRequireLinkedEmployee;

public sealed class RenameChecklistItemCommandValidator : CustomValidator<RenameChecklistItemCommand>
{
    public RenameChecklistItemCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.TaskId).NotEmpty();
        RuleFor(c => c.ItemId).NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128);
    }
}

public sealed class RenameChecklistItemCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier, ILogger<RenameChecklistItemCommandHandler> logger) : ICommandHandler<RenameChecklistItemCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<RenameChecklistItemCommandHandler> _logger = logger;

    public async Task<Result> Handle(RenameChecklistItemCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .IncludeAccess()
                .Include(m => m.Goals).ThenInclude(g => g.Steps).ThenInclude(s => s.Tasks)
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure("Story map not found.");

            var actor = await _access.ResolveActor(map, cancellationToken);

            var result = map.RenameChecklistItem(request.TaskId, request.ItemId, request.Name, actor);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            var taskDto = map.Goals.SelectMany(g => g.Steps).SelectMany(s => s.Tasks)
                .First(t => t.Id == request.TaskId)
                .Adapt<StoryMapTaskDto>();

            await _notifier.NotifyTaskChecklistChanged(map.Id, taskDto);

            return Result.Success();
        }
        catch (Exception ex) when (ex is not ForbiddenException)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
