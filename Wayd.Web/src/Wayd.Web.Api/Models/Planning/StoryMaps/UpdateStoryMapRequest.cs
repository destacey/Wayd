using Wayd.Planning.Application.StoryMaps.Commands;

namespace Wayd.Web.Api.Models.Planning.StoryMaps;

public class UpdateStoryMapRequest
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    public UpdateStoryMapCommand ToUpdateStoryMapCommand(Guid id)
    {
        return new UpdateStoryMapCommand(id, Name, Description);
    }
}

public sealed class UpdateStoryMapRequestValidator : CustomValidator<UpdateStoryMapRequest>
{
    public UpdateStoryMapRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
        RuleFor(r => r.Description).MaximumLength(2048);
    }
}
