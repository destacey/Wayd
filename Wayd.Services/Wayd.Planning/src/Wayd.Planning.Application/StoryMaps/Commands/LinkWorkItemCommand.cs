using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record LinkWorkItemCommand(Guid StoryMapId, Guid TaskId, int WorkItemId) : ICommand;

public sealed class LinkWorkItemCommandValidator : CustomValidator<LinkWorkItemCommand>
{
    public LinkWorkItemCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.TaskId).NotEmpty();
        RuleFor(c => c.WorkItemId).GreaterThan(0);
    }
}

public sealed class LinkWorkItemCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<LinkWorkItemCommandHandler> logger) : ICommandHandler<LinkWorkItemCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<LinkWorkItemCommandHandler> _logger = logger;

    public async Task<Result> Handle(LinkWorkItemCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .Include(m => m.Goals).ThenInclude(g => g.Steps).ThenInclude(s => s.Tasks)
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure("Story map not found.");

            var result = map.LinkWorkItem(request.TaskId, request.WorkItemId);
            if (result.IsFailure)
                return Result.Failure(result.Error);

            await _planningDbContext.SaveChangesAsync(cancellationToken);
            await _notifier.NotifyTaskWorkItemLinked(map.Id, request.TaskId, request.WorkItemId);

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
