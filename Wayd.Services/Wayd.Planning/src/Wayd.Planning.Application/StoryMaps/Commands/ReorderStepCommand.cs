using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record ReorderStepCommand(Guid StoryMapId, Guid StepId, int NewOrder) : ICommand, IRequireLinkedEmployee;

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

public sealed class ReorderStepCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier, ILogger<ReorderStepCommandHandler> logger) : ICommandHandler<ReorderStepCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<ReorderStepCommandHandler> _logger = logger;

    public async Task<Result> Handle(ReorderStepCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await StoryMapMutation.Apply(
                _planningDbContext,
                _access,
                ct => _planningDbContext.StoryMaps
                    .IncludeAccess()
                    .Include(m => m.Goals).ThenInclude(g => g.Steps)
                    .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, ct),
                (map, actor) => map.ReorderStep(request.StepId, request.NewOrder, actor)
                    .Map(() => map.Goals
                        .SelectMany(g => g.Steps)
                        .First(s => s.Id == request.StepId)
                        .Order),
                cancellationToken);

            if (result.IsFailure)
                return Result.Failure(result.Error);

            await _notifier.NotifyStepReordered(request.StoryMapId, request.StepId, result.Value);

            return Result.Success();
        }
        catch (Exception ex) when (ex is not ForbiddenException)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name}", requestName);
            return Result.Failure($"Wayd Request: Exception for Request {requestName}");
        }
    }
}
