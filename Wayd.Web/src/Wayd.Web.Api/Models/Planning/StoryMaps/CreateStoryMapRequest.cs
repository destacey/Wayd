using Wayd.Planning.Application.StoryMaps.Commands;

namespace Wayd.Web.Api.Models.Planning.StoryMaps;

public class CreateStoryMapRequest
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    public CreateStoryMapCommand ToCreateStoryMapCommand()
    {
        return new CreateStoryMapCommand(Name, Description);
    }
}

public sealed class CreateStoryMapRequestValidator : CustomValidator<CreateStoryMapRequest>
{
    public CreateStoryMapRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
        RuleFor(r => r.Description).MaximumLength(2048);
    }
}
