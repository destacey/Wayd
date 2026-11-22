using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record AddPersonaCommand(Guid StoryMapId, string Name, string? Description, string Color) : ICommand<StoryMapPersonaDto>, IRequireLinkedEmployee;

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
            .IsHexColor();
    }
}

public sealed class AddPersonaCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier) : ICommandHandler<AddPersonaCommand, StoryMapPersonaDto>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;

    public async Task<Result<StoryMapPersonaDto>> Handle(AddPersonaCommand request, CancellationToken cancellationToken)
    {
        var map = await _planningDbContext.StoryMaps
            .IncludeAccess()
            .Include(m => m.Personas)
            .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

        if (map is null)
            return Result.Failure<StoryMapPersonaDto>("Story map not found.");

        var actor = await _access.ResolveActor(map, cancellationToken);

        var result = map.AddPersona(request.Name, request.Description, request.Color, actor);
        if (result.IsFailure)
            return Result.Failure<StoryMapPersonaDto>(result.Error);

        await _planningDbContext.SaveChangesAsync(cancellationToken);

        var dto = result.Value.Adapt<StoryMapPersonaDto>();
        await _notifier.NotifyPersonaAdded(map.Id, dto);

        return Result.Success(dto);
    }
}
