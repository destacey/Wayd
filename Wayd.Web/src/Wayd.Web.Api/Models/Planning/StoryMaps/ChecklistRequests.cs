using Wayd.Planning.Application.StoryMaps.Commands;

namespace Wayd.Web.Api.Models.Planning.StoryMaps;

public class AddChecklistItemRequest
{
    public string Name { get; set; } = default!;

    public AddChecklistItemCommand ToCommand(Guid storyMapId, Guid taskId) => new(storyMapId, taskId, Name);
}

public sealed class AddChecklistItemRequestValidator : CustomValidator<AddChecklistItemRequest>
{
    public AddChecklistItemRequestValidator()
    {
        // Matches the command validator, and the task-title limit a promoted item has to fit.
        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
    }
}

public class RenameChecklistItemRequest
{
    public string Name { get; set; } = default!;

    public RenameChecklistItemCommand ToCommand(Guid storyMapId, Guid taskId, Guid itemId) => new(storyMapId, taskId, itemId, Name);
}

public sealed class RenameChecklistItemRequestValidator : CustomValidator<RenameChecklistItemRequest>
{
    public RenameChecklistItemRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
    }
}

public class SetChecklistItemCheckedRequest
{
    public bool IsChecked { get; set; }

    public SetChecklistItemCheckedCommand ToCommand(Guid storyMapId, Guid taskId, Guid itemId) => new(storyMapId, taskId, itemId, IsChecked);
}
