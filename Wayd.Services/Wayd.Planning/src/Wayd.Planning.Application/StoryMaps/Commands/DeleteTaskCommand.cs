using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record DeleteTaskCommand(Guid StoryMapId, Guid TaskId) : ICommand;

public sealed class DeleteTaskCommandValidator : CustomValidator<DeleteTaskCommand>
{
    public DeleteTaskCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.TaskId).NotEmpty();
    }
}

public sealed class DeleteTaskCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<DeleteTaskCommandHandler> logger) : ICommandHandler<DeleteTaskCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<DeleteTaskCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteTaskCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .Include(m => m.Goals).ThenInclude(g => g.Steps).ThenInclude(s => s.Tasks)
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure("Story map not found.");

            var result = map.DeleteTask(request.TaskId);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);
            await _notifier.NotifyTaskDeleted(map.Id, request.TaskId);

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
