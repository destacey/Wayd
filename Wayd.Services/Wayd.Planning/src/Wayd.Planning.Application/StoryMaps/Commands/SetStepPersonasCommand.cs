using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record SetStepPersonasCommand(Guid StoryMapId, Guid StepId, IReadOnlyList<Guid> PersonaIds) : ICommand, IRequireLinkedEmployee;

public sealed class SetStepPersonasCommandValidator : CustomValidator<SetStepPersonasCommand>
{
    public SetStepPersonasCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.StepId).NotEmpty();
        RuleFor(c => c.PersonaIds).NotNull();
    }
}

public sealed class SetStepPersonasCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier) : ICommandHandler<SetStepPersonasCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;

    public async Task<Result> Handle(SetStepPersonasCommand request, CancellationToken cancellationToken)
    {
        var map = await _planningDbContext.StoryMaps
            .IncludeAccess()
            .Include(m => m.Goals).ThenInclude(g => g.Steps)
            .Include(m => m.Personas)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

        if (map is null)
            return Result.Failure("Story map not found.");

        var actor = await _access.ResolveActor(map, cancellationToken);

        var result = map.SetStepPersonas(request.StepId, request.PersonaIds, actor);
        if (result.IsFailure)
            return result;

        await _planningDbContext.SaveChangesAsync(cancellationToken);
        var appliedPersonaIds = map.Goals
            .SelectMany(g => g.Steps)
            .First(s => s.Id == request.StepId)
            .PersonaIds;
        await _notifier.NotifyStepPersonasChanged(map.Id, request.StepId, appliedPersonaIds);

        return Result.Success();
    }
}
