using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record UpdatePersonaCommand(Guid StoryMapId, Guid PersonaId, string Name, string? Description, string Color) : ICommand;

public sealed class UpdatePersonaCommandValidator : CustomValidator<UpdatePersonaCommand>
{
    public UpdatePersonaCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.PersonaId).NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(c => c.Description)
            .MaximumLength(256);

        RuleFor(c => c.Color)
            .NotEmpty()
            .MaximumLength(7);
    }
}

public sealed class UpdatePersonaCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<UpdatePersonaCommandHandler> logger) : ICommandHandler<UpdatePersonaCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<UpdatePersonaCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdatePersonaCommand request, CancellationToken cancellationToken)
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

            var result = map.UpdatePersona(request.PersonaId, request.Name, request.Description, request.Color);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            var dto = map.Personas.First(p => p.Id == request.PersonaId).Adapt<StoryMapPersonaDto>();
            await _notifier.NotifyPersonaUpdated(map.Id, dto);

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
