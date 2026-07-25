using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record RenameSwimLaneCommand(Guid StoryMapId, Guid SwimLaneId, string Name) : ICommand;

public sealed class RenameSwimLaneCommandValidator : CustomValidator<RenameSwimLaneCommand>
{
    public RenameSwimLaneCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.SwimLaneId).NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128);
    }
}

public sealed class RenameSwimLaneCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<RenameSwimLaneCommandHandler> logger) : ICommandHandler<RenameSwimLaneCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<RenameSwimLaneCommandHandler> _logger = logger;

    public async Task<Result> Handle(RenameSwimLaneCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .Include(m => m.SwimLanes)
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure("Story map not found.");

            var result = map.RenameSwimLane(request.SwimLaneId, request.Name);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);
            await _notifier.NotifySwimLaneRenamed(map.Id, request.SwimLaneId, request.Name);

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
