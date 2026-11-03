using Wayd.Common.Domain.Enums;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Web.Api.Models.Planning.StoryMaps;

/// <summary>A new story map, with who owns it, who can edit it and who can see it.</summary>
public class CreateStoryMapRequest
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    /// <summary>
    /// `Public` (the default): anyone with the Story Maps View permission can see the map. `Private`: only its
    /// owners, editors and the members of its editor teams.
    /// </summary>
    public Visibility Visibility { get; set; } = Visibility.Public;

    /// <summary>
    /// Who can change a public map's content: `AnyoneWhoCanView` (the default), or `OwnersAndEditors`.
    /// </summary>
    public StoryMapEditAccess EditAccess { get; set; } = StoryMapEditAccess.AnyoneWhoCanView;

    /// <summary>
    /// Employees who own the map alongside the caller, who is always an owner.
    /// </summary>
    public List<Guid>? OwnerIds { get; set; }

    /// <summary>
    /// Employees invited to edit the map.
    /// </summary>
    public List<Guid>? EditorIds { get; set; }

    /// <summary>
    /// Teams whose current members are invited to edit the map.
    /// </summary>
    public List<Guid>? EditorTeamIds { get; set; }

    /// <summary>The create command; the caller is added as an owner by the handler.</summary>
    public CreateStoryMapCommand ToCreateStoryMapCommand()
    {
        return new CreateStoryMapCommand(Name, Description, Visibility, EditAccess, OwnerIds, EditorIds, EditorTeamIds);
    }
}

/// <summary>Checks the name, description, visibility and edit access.</summary>
public sealed class CreateStoryMapRequestValidator : CustomValidator<CreateStoryMapRequest>
{
    public CreateStoryMapRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
        RuleFor(r => r.Description).MaximumLength(2048);
        RuleFor(r => r.Visibility).IsInEnum();
        RuleFor(r => r.EditAccess).IsInEnum();
    }
}
