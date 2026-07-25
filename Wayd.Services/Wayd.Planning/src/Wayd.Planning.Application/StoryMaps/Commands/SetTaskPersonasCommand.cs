using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record SetTaskPersonasCommand(Guid StoryMapId, Guid TaskId, IReadOnlyList<Guid> PersonaIds) : ICommand;

public sealed class SetTaskPersonasCommandValidator : CustomValidator<SetTaskPersonasCommand>
{
    public SetTaskPersonasCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.TaskId).NotEmpty();
        RuleFor(c => c.PersonaIds).NotNull();
    }
}

public sealed class SetTaskPersonasCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<SetTaskPersonasCommandHandler> logger) : ICommandHandler<SetTaskPersonasCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<SetTaskPersonasCommandHandler> _logger = logger;

    public async Task<Result> Handle(SetTaskPersonasCommand request, CancellationToken cancellationToken)
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

            var result = map.SetTaskPersonas(request.TaskId, request.PersonaIds);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);
            await _notifier.NotifyTaskPersonasChanged(map.Id, request.TaskId, request.PersonaIds);

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
