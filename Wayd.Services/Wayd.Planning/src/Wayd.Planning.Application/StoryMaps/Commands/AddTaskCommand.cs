using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record AddTaskCommand(Guid StoryMapId, Guid StepId, string Title, Guid? SwimLaneId) : ICommand<StoryMapTaskDto>;

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

public sealed class AddTaskCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<AddTaskCommandHandler> logger) : ICommandHandler<AddTaskCommand, StoryMapTaskDto>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<AddTaskCommandHandler> _logger = logger;

    public async Task<Result<StoryMapTaskDto>> Handle(AddTaskCommand request, CancellationToken cancellationToken)
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

            var result = map.AddTask(request.StepId, request.Title, request.SwimLaneId);
            if (result.IsFailure)
                return Result.Failure<StoryMapTaskDto>(result.Error);

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            var dto = result.Value.Adapt<StoryMapTaskDto>();
            await _notifier.NotifyTaskAdded(map.Id, dto);

            return Result.Success(dto);
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure<StoryMapTaskDto>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
