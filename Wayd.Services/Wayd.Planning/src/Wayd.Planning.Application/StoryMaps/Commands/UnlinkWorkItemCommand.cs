using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record UnlinkWorkItemCommand(Guid StoryMapId, Guid TaskId) : ICommand;

public sealed class UnlinkWorkItemCommandValidator : CustomValidator<UnlinkWorkItemCommand>
{
    public UnlinkWorkItemCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.TaskId).NotEmpty();
    }
}

public sealed class UnlinkWorkItemCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<UnlinkWorkItemCommandHandler> logger) : ICommandHandler<UnlinkWorkItemCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<UnlinkWorkItemCommandHandler> _logger = logger;

    public async Task<Result> Handle(UnlinkWorkItemCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .Include(m => m.Goals).ThenInclude(g => g.Steps).ThenInclude(s => s.Tasks)
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure("Story map not found.");

            var result = map.UnlinkWorkItem(request.TaskId);
            if (result.IsFailure)
                return Result.Failure(result.Error);

            await _planningDbContext.SaveChangesAsync(cancellationToken);
            await _notifier.NotifyTaskWorkItemUnlinked(map.Id, request.TaskId);

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
