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

public sealed class ReorderStepCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier) : ICommandHandler<ReorderStepCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;

    public async Task<Result> Handle(ReorderStepCommand request, CancellationToken cancellationToken)
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
}
