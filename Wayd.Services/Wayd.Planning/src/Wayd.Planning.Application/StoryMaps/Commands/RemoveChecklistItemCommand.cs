using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record RemoveChecklistItemCommand(Guid StoryMapId, Guid TaskId, Guid ItemId) : ICommand;

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

public sealed class RemoveChecklistItemCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<RemoveChecklistItemCommandHandler> logger) : ICommandHandler<RemoveChecklistItemCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<RemoveChecklistItemCommandHandler> _logger = logger;

    public async Task<Result> Handle(RemoveChecklistItemCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .Include(m => m.Goals).ThenInclude(g => g.Steps).ThenInclude(s => s.Tasks)
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure("Story map not found.");

            var result = map.RemoveChecklistItem(request.TaskId, request.ItemId);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            var taskDto = map.Goals.SelectMany(g => g.Steps).SelectMany(s => s.Tasks)
                .First(t => t.Id == request.TaskId)
                .Adapt<StoryMapTaskDto>();

            await _notifier.NotifyTaskChecklistChanged(map.Id, taskDto);

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
