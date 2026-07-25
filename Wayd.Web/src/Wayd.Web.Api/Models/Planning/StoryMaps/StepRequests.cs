using Wayd.Planning.Application.StoryMaps.Commands;

namespace Wayd.Web.Api.Models.Planning.StoryMaps;

public class AddStepRequest
{
    public Guid GoalId { get; set; }
    public string Name { get; set; } = default!;

    public AddStepCommand ToCommand(Guid storyMapId) => new(storyMapId, GoalId, Name);
}

public sealed class AddStepRequestValidator : CustomValidator<AddStepRequest>
{
    public AddStepRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(r => r.GoalId).NotEmpty();
        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
    }
}

public class RenameStepRequest
{
    public string Name { get; set; } = default!;

    public RenameStepCommand ToCommand(Guid storyMapId, Guid stepId) => new(storyMapId, stepId, Name);
}

public sealed class RenameStepRequestValidator : CustomValidator<RenameStepRequest>
{
    public RenameStepRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
    }
}

public class ReorderStepRequest
{
    public int NewOrder { get; set; }

    public ReorderStepCommand ToCommand(Guid storyMapId, Guid stepId) => new(storyMapId, stepId, NewOrder);
}

public class MoveStepRequest
{
    public Guid TargetGoalId { get; set; }
    public int NewOrder { get; set; }

    public MoveStepCommand ToCommand(Guid storyMapId, Guid stepId) => new(storyMapId, stepId, TargetGoalId, NewOrder);
}

public sealed class MoveStepRequestValidator : CustomValidator<MoveStepRequest>
{
    public MoveStepRequestValidator()
    {
        RuleFor(r => r.TargetGoalId).NotEmpty();
    }
}
