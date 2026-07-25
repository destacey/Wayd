using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record AddPersonaCommand(Guid StoryMapId, string Name, string? Description, string Color) : ICommand<StoryMapPersonaDto>;

public sealed class AddPersonaCommandValidator : CustomValidator<AddPersonaCommand>
{
    public AddPersonaCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();

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

public sealed class AddPersonaCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<AddPersonaCommandHandler> logger) : ICommandHandler<AddPersonaCommand, StoryMapPersonaDto>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<AddPersonaCommandHandler> _logger = logger;

    public async Task<Result<StoryMapPersonaDto>> Handle(AddPersonaCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .Include(m => m.Goals).ThenInclude(g => g.Steps).ThenInclude(s => s.Tasks)
                .Include(m => m.SwimLanes)
                .Include(m => m.Personas)
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure<StoryMapPersonaDto>("Story map not found.");

            var result = map.AddPersona(request.Name, request.Description, request.Color);
            if (result.IsFailure)
                return Result.Failure<StoryMapPersonaDto>(result.Error);

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            var dto = result.Value.Adapt<StoryMapPersonaDto>();
            await _notifier.NotifyPersonaAdded(map.Id, dto);

            return Result.Success(dto);
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure<StoryMapPersonaDto>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
