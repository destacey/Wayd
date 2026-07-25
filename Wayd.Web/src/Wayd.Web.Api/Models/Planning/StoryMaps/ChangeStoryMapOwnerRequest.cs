using Wayd.Planning.Application.StoryMaps.Commands;

namespace Wayd.Web.Api.Models.Planning.StoryMaps;

public class ChangeStoryMapOwnerRequest
{
    public string OwnerId { get; set; } = default!;

    public ChangeStoryMapOwnerCommand ToChangeStoryMapOwnerCommand(Guid id)
    {
        return new ChangeStoryMapOwnerCommand(id, OwnerId);
    }
}

public sealed class ChangeStoryMapOwnerRequestValidator : CustomValidator<ChangeStoryMapOwnerRequest>
{
    public ChangeStoryMapOwnerRequestValidator()
    {
        RuleFor(r => r.OwnerId).NotEmpty();
    }
}
