using Wayd.Planning.Application.StoryMaps.Commands;

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
    public IReadOnlyList<Guid> PersonaIds { get; set; } = [];

    public SetGoalPersonasCommand ToCommand(Guid storyMapId, Guid goalId) => new(storyMapId, goalId, PersonaIds);
}

public class SetStepPersonasRequest
{
    public IReadOnlyList<Guid> PersonaIds { get; set; } = [];

    public SetStepPersonasCommand ToCommand(Guid storyMapId, Guid stepId) => new(storyMapId, stepId, PersonaIds);
}
