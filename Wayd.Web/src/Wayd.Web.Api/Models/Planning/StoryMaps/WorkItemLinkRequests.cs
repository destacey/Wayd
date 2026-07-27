using Wayd.Planning.Application.StoryMaps.Commands;

namespace Wayd.Web.Api.Models.Planning.StoryMaps;

public class LinkWorkItemRequest
{
    public int WorkItemId { get; set; }

    public LinkWorkItemCommand ToCommand(Guid storyMapId, Guid taskId) => new(storyMapId, taskId, WorkItemId);
}

public sealed class LinkWorkItemRequestValidator : CustomValidator<LinkWorkItemRequest>
{
    public LinkWorkItemRequestValidator()
    {
        RuleFor(r => r.WorkItemId).GreaterThan(0);
    }
}
