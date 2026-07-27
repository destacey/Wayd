using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record PromoteChecklistItemCommand(Guid StoryMapId, Guid TaskId, Guid ItemId) : ICommand<StoryMapTaskDto>;

public sealed class PromoteChecklistItemCommandValidator : CustomValidator<PromoteChecklistItemCommand>
{
    public PromoteChecklistItemCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.TaskId).NotEmpty();
        RuleFor(c => c.ItemId).NotEmpty();
    }
}

public sealed class PromoteChecklistItemCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<PromoteChecklistItemCommandHandler> logger) : ICommandHandler<PromoteChecklistItemCommand, StoryMapTaskDto>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<PromoteChecklistItemCommandHandler> _logger = logger;

    public async Task<Result<StoryMapTaskDto>> Handle(PromoteChecklistItemCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .Include(m => m.Goals).ThenInclude(g => g.Steps).ThenInclude(s => s.Tasks)
                .Include(m => m.SwimLanes)
                .AsSplitQuery()
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure<StoryMapTaskDto>("Story map not found.");

            var result = map.PromoteChecklistItem(request.TaskId, request.ItemId);
            if (result.IsFailure)
                return Result.Failure<StoryMapTaskDto>(result.Error);

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            var newTaskDto = result.Value.Adapt<StoryMapTaskDto>();

            var sourceTaskDto = map.Goals.SelectMany(g => g.Steps).SelectMany(s => s.Tasks)
                .First(t => t.Id == request.TaskId)
                .Adapt<StoryMapTaskDto>();

            await _notifier.NotifyChecklistItemPromoted(map.Id, newTaskDto, sourceTaskDto);

            return Result.Success(newTaskDto);
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure<StoryMapTaskDto>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
