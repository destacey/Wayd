using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record SetGoalPersonasCommand(Guid StoryMapId, Guid GoalId, IReadOnlyList<Guid> PersonaIds) : ICommand, IRequireLinkedEmployee;

public sealed class SetGoalPersonasCommandValidator : CustomValidator<SetGoalPersonasCommand>
{
    public SetGoalPersonasCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.GoalId).NotEmpty();
    }
}

public sealed class SetGoalPersonasCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier, ILogger<SetGoalPersonasCommandHandler> logger) : ICommandHandler<SetGoalPersonasCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<SetGoalPersonasCommandHandler> _logger = logger;

    public async Task<Result> Handle(SetGoalPersonasCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .IncludeAccess()
                .Include(m => m.Goals)
                .Include(m => m.Personas)
                .AsSplitQuery()
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure("Story map not found.");

            var actor = await _access.ResolveActor(map, cancellationToken);

            var result = map.SetGoalPersonas(request.GoalId, request.PersonaIds, actor);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);
            var appliedPersonaIds = map.Goals.First(g => g.Id == request.GoalId).PersonaIds;
            await _notifier.NotifyGoalPersonasChanged(map.Id, request.GoalId, appliedPersonaIds);

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
