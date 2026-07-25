using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record ReorderStepCommand(Guid StoryMapId, Guid StepId, int NewOrder) : ICommand;

public sealed class ReorderStepCommandValidator : CustomValidator<ReorderStepCommand>
{
    public ReorderStepCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();

        RuleFor(c => c.StepId).NotEmpty();

        RuleFor(c => c.NewOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class ReorderStepCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<ReorderStepCommandHandler> logger) : ICommandHandler<ReorderStepCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<ReorderStepCommandHandler> _logger = logger;

    public async Task<Result> Handle(ReorderStepCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .Include(m => m.Goals).ThenInclude(g => g.Steps).ThenInclude(s => s.Tasks)
                .Include(m => m.SwimLanes)
                .Include(m => m.Personas)
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure("Story map not found.");

            var result = map.ReorderStep(request.StepId, request.NewOrder);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);
            await _notifier.NotifyStepReordered(map.Id, request.StepId, request.NewOrder);

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
