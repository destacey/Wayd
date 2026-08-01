using Wayd.Planning.Application.StoryMaps.Commands;

namespace Wayd.Web.Api.Models.Planning.StoryMaps;

public class AddTaskRequest
{
    public Guid StepId { get; set; }
    public string Title { get; set; } = default!;
    public Guid? SwimLaneId { get; set; }

    public AddTaskCommand ToCommand(Guid storyMapId) => new(storyMapId, StepId, Title, SwimLaneId);
}

public sealed class AddTaskRequestValidator : CustomValidator<AddTaskRequest>
{
    public AddTaskRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(r => r.StepId).NotEmpty();
        RuleFor(r => r.Title).NotEmpty().MaximumLength(128);
    }
}

public class UpdateTaskRequest
{
    public string Title { get; set; } = default!;
    public string? Description { get; set; }

    public UpdateTaskCommand ToCommand(Guid storyMapId, Guid taskId) => new(storyMapId, taskId, Title, Description);
}

public sealed class UpdateTaskRequestValidator : CustomValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(r => r.Title).NotEmpty().MaximumLength(128);
        RuleFor(r => r.Description).MaximumLength(2048);
    }
}

public class RenameTaskRequest
{
    public string Title { get; set; } = default!;

    public RenameTaskCommand ToCommand(Guid storyMapId, Guid taskId) => new(storyMapId, taskId, Title);
}

public sealed class RenameTaskRequestValidator : CustomValidator<RenameTaskRequest>
{
    public RenameTaskRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(r => r.Title).NotEmpty().MaximumLength(128);
    }
}

public class SetTaskDescriptionRequest
{
    public string? Description { get; set; }

    public SetTaskDescriptionCommand ToCommand(Guid storyMapId, Guid taskId) => new(storyMapId, taskId, Description);
}

public sealed class SetTaskDescriptionRequestValidator : CustomValidator<SetTaskDescriptionRequest>
{
    public SetTaskDescriptionRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(r => r.Description).MaximumLength(2048);
    }
}

public class MoveTaskRequest
{
    public Guid TargetStepId { get; set; }
    public Guid TargetSwimLaneId { get; set; }
    public int NewOrder { get; set; }

    public MoveTaskCommand ToCommand(Guid storyMapId, Guid taskId) => new(storyMapId, taskId, TargetStepId, TargetSwimLaneId, NewOrder);
}

public sealed class MoveTaskRequestValidator : CustomValidator<MoveTaskRequest>
{
    public MoveTaskRequestValidator()
    {
        RuleFor(r => r.TargetStepId).NotEmpty();
        RuleFor(r => r.TargetSwimLaneId).NotEmpty();
    }
}

public class SetTaskPersonasRequest
{
    public IReadOnlyList<Guid> PersonaIds { get; set; } = [];

    public SetTaskPersonasCommand ToCommand(Guid storyMapId, Guid taskId) => new(storyMapId, taskId, PersonaIds);
}
