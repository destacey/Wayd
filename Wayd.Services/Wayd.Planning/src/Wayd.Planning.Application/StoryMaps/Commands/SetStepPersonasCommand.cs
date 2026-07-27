using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record SetStepPersonasCommand(Guid StoryMapId, Guid StepId, IReadOnlyList<Guid> PersonaIds) : ICommand;

public sealed class SetStepPersonasCommandValidator : CustomValidator<SetStepPersonasCommand>
{
    public SetStepPersonasCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.StepId).NotEmpty();
    }
}

public sealed class SetStepPersonasCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<SetStepPersonasCommandHandler> logger) : ICommandHandler<SetStepPersonasCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<SetStepPersonasCommandHandler> _logger = logger;

    public async Task<Result> Handle(SetStepPersonasCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .Include(m => m.Goals).ThenInclude(g => g.Steps)
                .Include(m => m.Personas)
                .AsSplitQuery()
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure("Story map not found.");

            var result = map.SetStepPersonas(request.StepId, request.PersonaIds);
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
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
