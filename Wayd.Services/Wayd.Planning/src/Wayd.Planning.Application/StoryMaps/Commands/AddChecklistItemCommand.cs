using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record AddChecklistItemCommand(Guid StoryMapId, Guid TaskId, string Name) : ICommand<StoryMapTaskDto>;

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

public sealed class AddChecklistItemCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<AddChecklistItemCommandHandler> logger) : ICommandHandler<AddChecklistItemCommand, StoryMapTaskDto>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<AddChecklistItemCommandHandler> _logger = logger;

    public async Task<Result<StoryMapTaskDto>> Handle(AddChecklistItemCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .Include(m => m.Goals).ThenInclude(g => g.Steps).ThenInclude(s => s.Tasks)
                .Include(m => m.SwimLanes)
                .Include(m => m.Personas)
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure<StoryMapTaskDto>("Story map not found.");

            var result = map.AddChecklistItem(request.TaskId, request.Name);
            if (result.IsFailure)
                return Result.Failure<StoryMapTaskDto>(result.Error);

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            var taskDto = map.Goals.SelectMany(g => g.Steps).SelectMany(s => s.Tasks)
                .First(t => t.Id == request.TaskId)
                .Adapt<StoryMapTaskDto>();

            await _notifier.NotifyTaskChecklistChanged(map.Id, taskDto);

            return Result.Success(taskDto);
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure<StoryMapTaskDto>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
