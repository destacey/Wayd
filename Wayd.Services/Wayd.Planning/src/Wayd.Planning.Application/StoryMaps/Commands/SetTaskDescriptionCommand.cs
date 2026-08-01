using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

/// <summary>
/// Sets a task's description without touching its title.
/// </summary>
/// <remarks>
/// Separate from <see cref="UpdateTaskCommand"/> so an editor changing only the description does not
/// send the title back and revert a concurrent rename.
/// </remarks>
public sealed record SetTaskDescriptionCommand(Guid StoryMapId, Guid TaskId, string? Description) : ICommand;

public sealed class SetTaskDescriptionCommandValidator : CustomValidator<SetTaskDescriptionCommand>
{
    public SetTaskDescriptionCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.TaskId).NotEmpty();

        RuleFor(c => c.Description)
            .MaximumLength(2048);
    }
}

public sealed class SetTaskDescriptionCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<SetTaskDescriptionCommandHandler> logger) : ICommandHandler<SetTaskDescriptionCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<SetTaskDescriptionCommandHandler> _logger = logger;

    public async Task<Result> Handle(SetTaskDescriptionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .Include(m => m.Goals).ThenInclude(g => g.Steps).ThenInclude(s => s.Tasks)
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure("Story map not found.");

            var result = map.SetTaskDescription(request.TaskId, request.Description);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            var task = map.Goals.SelectMany(g => g.Steps).SelectMany(s => s.Tasks).First(t => t.Id == request.TaskId);
            var dto = task.Adapt<StoryMapTaskDto>();
            await _notifier.NotifyTaskUpdated(map.Id, dto);

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
