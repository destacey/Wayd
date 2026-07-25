using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record DeletePersonaCommand(Guid StoryMapId, Guid PersonaId) : ICommand<int>;

public sealed class DeletePersonaCommandValidator : CustomValidator<DeletePersonaCommand>
{
    public DeletePersonaCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.PersonaId).NotEmpty();
    }
}

public sealed class DeletePersonaCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<DeletePersonaCommandHandler> logger) : ICommandHandler<DeletePersonaCommand, int>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<DeletePersonaCommandHandler> _logger = logger;

    public async Task<Result<int>> Handle(DeletePersonaCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .Include(m => m.Goals).ThenInclude(g => g.Steps).ThenInclude(s => s.Tasks)
                .Include(m => m.SwimLanes)
                .Include(m => m.Personas)
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure<int>("Story map not found.");

            var result = map.DeletePersona(request.PersonaId);
            if (result.IsFailure)
                return Result.Failure<int>(result.Error);

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            var untaggedCount = result.Value;
            await _notifier.NotifyPersonaDeleted(map.Id, request.PersonaId, untaggedCount);

            return Result.Success(untaggedCount);
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure<int>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
