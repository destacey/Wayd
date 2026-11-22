using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record ReorderPersonaCommand(Guid StoryMapId, Guid PersonaId, int NewOrder) : ICommand, IRequireLinkedEmployee;

public sealed class ReorderPersonaCommandValidator : CustomValidator<ReorderPersonaCommand>
{
    public ReorderPersonaCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.PersonaId).NotEmpty();
        RuleFor(c => c.NewOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class ReorderPersonaCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier) : ICommandHandler<ReorderPersonaCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;

    public async Task<Result> Handle(ReorderPersonaCommand request, CancellationToken cancellationToken)
    {
        var result = await StoryMapMutation.Apply(
            _planningDbContext,
            _access,
            ct => _planningDbContext.StoryMaps
                .IncludeAccess()
                .Include(m => m.Personas)
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, ct),
            (map, actor) => map.ReorderPersona(request.PersonaId, request.NewOrder, actor)
                .Map(() => map.Personas.First(p => p.Id == request.PersonaId).Order),
            cancellationToken);

        if (result.IsFailure)
            return Result.Failure(result.Error);

        await _notifier.NotifyPersonaReordered(request.StoryMapId, request.PersonaId, result.Value);

        return Result.Success();
    }
}
