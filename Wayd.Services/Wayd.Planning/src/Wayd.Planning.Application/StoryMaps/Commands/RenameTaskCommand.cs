using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

/// <summary>
/// Renames a task without touching its description.
/// </summary>
/// <remarks>
/// Separate from <see cref="UpdateTaskCommand"/> so an editor changing only the title does not send
/// the description back and revert a concurrent edit to it.
/// </remarks>
public sealed record RenameTaskCommand(Guid StoryMapId, Guid TaskId, string Title) : ICommand;

public sealed class RenameTaskCommandValidator : CustomValidator<RenameTaskCommand>
{
    public RenameTaskCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.TaskId).NotEmpty();

        RuleFor(c => c.Title)
            .NotEmpty()
            .MaximumLength(128);
    }
}

public sealed class RenameTaskCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<RenameTaskCommandHandler> logger) : ICommandHandler<RenameTaskCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<RenameTaskCommandHandler> _logger = logger;

    public async Task<Result> Handle(RenameTaskCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .Include(m => m.Goals).ThenInclude(g => g.Steps).ThenInclude(s => s.Tasks)
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure("Story map not found.");

            var result = map.RenameTask(request.TaskId, request.Title);
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
