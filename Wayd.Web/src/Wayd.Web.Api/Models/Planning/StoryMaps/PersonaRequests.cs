using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Dtos;

namespace Wayd.Web.Api.Models.Planning.StoryMaps;

public class AddPersonaRequest
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string Color { get; set; } = default!;

    public AddPersonaCommand ToCommand(Guid storyMapId) => new(storyMapId, Name, Description, Color);
}

public sealed class AddPersonaRequestValidator : CustomValidator<AddPersonaRequest>
{
    public AddPersonaRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
        RuleFor(r => r.Description).MaximumLength(256);
        RuleFor(r => r.Color).NotEmpty()
            .IsHexColor();
    }
}

public class UpdatePersonaRequest
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string Color { get; set; } = default!;

    public UpdatePersonaCommand ToCommand(Guid storyMapId, Guid personaId) => new(storyMapId, personaId, Name, Description, Color);
}

public sealed class UpdatePersonaRequestValidator : CustomValidator<UpdatePersonaRequest>
{
    public UpdatePersonaRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
        RuleFor(r => r.Description).MaximumLength(256);
        RuleFor(r => r.Color).NotEmpty()
            .IsHexColor();
    }
}

public class ReorderPersonaRequest
{
    public int NewOrder { get; set; }

    public ReorderPersonaCommand ToCommand(Guid storyMapId, Guid personaId) => new(storyMapId, personaId, NewOrder);
}

public sealed class ReorderPersonaRequestValidator : CustomValidator<ReorderPersonaRequest>
{
    public ReorderPersonaRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(r => r.NewOrder).GreaterThanOrEqualTo(0);
    }
}

public class SetGoalPersonasRequest
{
    /// <summary>
    /// Every persona tagged on the goal. A list rather than a read-only one so a JSON Patch can append to it.
    /// </summary>
    public List<Guid> PersonaIds { get; set; } = [];

    public SetGoalPersonasCommand ToCommand(Guid storyMapId, Guid goalId) => new(storyMapId, goalId, PersonaIds);

    /// <summary>
    /// The personas <paramref name="dto"/> is tagged with now: the record a JSON Patch is applied to.
    /// </summary>
    public static SetGoalPersonasRequest FromDto(StoryMapGoalDto dto) => new() { PersonaIds = [.. dto.PersonaIds] };
}

/// <summary>
/// Rejects an empty persona id, so a malformed patch fails validation rather than in the domain.
/// </summary>
public sealed class SetGoalPersonasRequestValidator : CustomValidator<SetGoalPersonasRequest>
{
    public SetGoalPersonasRequestValidator()
    {
        RuleFor(r => r.PersonaIds)
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("PersonaIds cannot contain empty GUIDs.");
    }
}

public class SetStepPersonasRequest
{
    /// <summary>
    /// Every persona tagged on the step. A list rather than a read-only one so a JSON Patch can append to it.
    /// </summary>
    public List<Guid> PersonaIds { get; set; } = [];

    public SetStepPersonasCommand ToCommand(Guid storyMapId, Guid stepId) => new(storyMapId, stepId, PersonaIds);

    /// <summary>
    /// The personas <paramref name="dto"/> is tagged with now: the record a JSON Patch is applied to.
    /// </summary>
    public static SetStepPersonasRequest FromDto(StoryMapStepDto dto) => new() { PersonaIds = [.. dto.PersonaIds] };
}

/// <summary>
/// Rejects an empty persona id, so a malformed patch fails validation rather than in the domain.
/// </summary>
public sealed class SetStepPersonasRequestValidator : CustomValidator<SetStepPersonasRequest>
{
    public SetStepPersonasRequestValidator()
    {
        RuleFor(r => r.PersonaIds)
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("PersonaIds cannot contain empty GUIDs.");
    }
}
