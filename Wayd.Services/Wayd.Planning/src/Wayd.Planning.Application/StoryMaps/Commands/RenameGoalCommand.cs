using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record RenameGoalCommand(Guid StoryMapId, Guid GoalId, string Name) : ICommand;

public sealed class RenameGoalCommandValidator : CustomValidator<RenameGoalCommand>
{
    public RenameGoalCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.GoalId).NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128);
    }
}

public sealed class RenameGoalCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<RenameGoalCommandHandler> logger) : ICommandHandler<RenameGoalCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<RenameGoalCommandHandler> _logger = logger;

    public async Task<Result> Handle(RenameGoalCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .Include(m => m.Goals)
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure("Story map not found.");

            var result = map.RenameGoal(request.GoalId, request.Name);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);
            await _notifier.NotifyGoalRenamed(map.Id, request.GoalId, request.Name);

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
