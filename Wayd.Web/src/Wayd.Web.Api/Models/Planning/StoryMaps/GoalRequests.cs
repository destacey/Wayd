using Wayd.Planning.Application.StoryMaps.Commands;

namespace Wayd.Web.Api.Models.Planning.StoryMaps;

public class AddGoalRequest
{
    public string Name { get; set; } = default!;

    public AddGoalCommand ToCommand(Guid storyMapId) => new(storyMapId, Name);
}

public sealed class AddGoalRequestValidator : CustomValidator<AddGoalRequest>
{
    public AddGoalRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
    }
}

public class RenameGoalRequest
{
    public string Name { get; set; } = default!;

    public RenameGoalCommand ToCommand(Guid storyMapId, Guid goalId) => new(storyMapId, goalId, Name);
}

public sealed class RenameGoalRequestValidator : CustomValidator<RenameGoalRequest>
{
    public RenameGoalRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
    }
}

public class ReorderGoalRequest
{
    public int NewOrder { get; set; }

    public ReorderGoalCommand ToCommand(Guid storyMapId, Guid goalId) => new(storyMapId, goalId, NewOrder);
}
