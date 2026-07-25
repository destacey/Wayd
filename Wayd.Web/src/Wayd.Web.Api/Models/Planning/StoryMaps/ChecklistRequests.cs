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
        RuleFor(r => r.Name).NotEmpty().MaximumLength(256);
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
        RuleFor(r => r.Name).NotEmpty().MaximumLength(256);
    }
}

public class SetChecklistItemCheckedRequest
{
    public bool IsChecked { get; set; }

    public SetChecklistItemCheckedCommand ToCommand(Guid storyMapId, Guid taskId, Guid itemId) => new(storyMapId, taskId, itemId, IsChecked);
}
