using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record DeleteStepCommand(Guid StoryMapId, Guid StepId) : ICommand, IRequireLinkedEmployee;

public sealed class DeleteStepCommandValidator : CustomValidator<DeleteStepCommand>
{
    public DeleteStepCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();

        RuleFor(c => c.StepId).NotEmpty();
    }
}

public sealed class DeleteStepCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier) : ICommandHandler<DeleteStepCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;

    public async Task<Result> Handle(DeleteStepCommand request, CancellationToken cancellationToken)
    {
        var map = await _planningDbContext.StoryMaps
            .IncludeAccess()
            .Include(m => m.Goals).ThenInclude(g => g.Steps)
            .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

        if (map is null)
            return Result.Failure("Story map not found.");

        var actor = await _access.ResolveActor(map, cancellationToken);

        var result = map.DeleteStep(request.StepId, actor);
        if (result.IsFailure)
            return result;

        await _planningDbContext.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyStepDeleted(map.Id, request.StepId);

        return Result.Success();
    }
}
