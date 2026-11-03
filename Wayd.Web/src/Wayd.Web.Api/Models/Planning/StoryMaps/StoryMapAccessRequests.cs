using Wayd.Common.Domain.Enums;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Web.Api.Models.Planning.StoryMaps;

/// <summary>The owners a story map has from now on.</summary>
public sealed class SetStoryMapOwnersRequest
{
    /// <summary>
    /// The employees who own the map from now on, replacing the current list. At least one.
    /// </summary>
    public List<Guid> EmployeeIds { get; set; } = [];

    /// <summary>The command for the map <paramref name="id"/>.</summary>
    public SetStoryMapOwnersCommand ToSetStoryMapOwnersCommand(Guid id) => new(id, EmployeeIds);
}

/// <summary>Requires at least one owner.</summary>
public sealed class SetStoryMapOwnersRequestValidator : CustomValidator<SetStoryMapOwnersRequest>
{
    public SetStoryMapOwnersRequestValidator()
    {
        RuleFor(r => r.EmployeeIds)
            .NotEmpty()
            .WithMessage("A story map needs at least one owner.");
    }
}

/// <summary>The people and teams invited to edit a story map from now on.</summary>
public sealed class SetStoryMapEditorsRequest
{
    /// <summary>
    /// The employees invited to edit the map, replacing the current list. Empty removes every employee editor.
    /// </summary>
    public List<Guid> EmployeeIds { get; set; } = [];

    /// <summary>
    /// The teams invited to edit the map, replacing the current list. Empty removes every team.
    /// </summary>
    public List<Guid> TeamIds { get; set; } = [];

    /// <summary>The command for the map <paramref name="id"/>.</summary>
    public SetStoryMapEditorsCommand ToSetStoryMapEditorsCommand(Guid id) => new(id, EmployeeIds, TeamIds);
}

/// <summary>Requires both lists, which may be empty.</summary>
public sealed class SetStoryMapEditorsRequestValidator : CustomValidator<SetStoryMapEditorsRequest>
{
    public SetStoryMapEditorsRequestValidator()
    {
        RuleFor(r => r.EmployeeIds).NotNull();
        RuleFor(r => r.TeamIds).NotNull();
    }
}

/// <summary>Who can see a story map from now on.</summary>
public sealed class SetStoryMapVisibilityRequest
{
    /// <summary>
    /// `Public`: anyone with the Story Maps View permission can see the map. `Private`: only its owners, editors
    /// and the members of its editor teams.
    /// </summary>
    public Visibility Visibility { get; set; }

    /// <summary>The command for the map <paramref name="id"/>.</summary>
    public SetStoryMapVisibilityCommand ToSetStoryMapVisibilityCommand(Guid id) => new(id, Visibility);
}

/// <summary>Rejects an unknown visibility.</summary>
public sealed class SetStoryMapVisibilityRequestValidator : CustomValidator<SetStoryMapVisibilityRequest>
{
    public SetStoryMapVisibilityRequestValidator()
    {
        RuleFor(r => r.Visibility).IsInEnum();
    }
}

/// <summary>Who can edit a public story map from now on.</summary>
public sealed class SetStoryMapEditAccessRequest
{
    /// <summary>
    /// Who can change a public map's content: `AnyoneWhoCanView`, or `OwnersAndEditors`. A private map admits only
    /// its owners and editors, so there it has no effect.
    /// </summary>
    public StoryMapEditAccess EditAccess { get; set; }

    /// <summary>The command for the map <paramref name="id"/>.</summary>
    public SetStoryMapEditAccessCommand ToSetStoryMapEditAccessCommand(Guid id) => new(id, EditAccess);
}

/// <summary>Rejects an unknown edit access.</summary>
public sealed class SetStoryMapEditAccessRequestValidator : CustomValidator<SetStoryMapEditAccessRequest>
{
    public SetStoryMapEditAccessRequestValidator()
    {
        RuleFor(r => r.EditAccess).IsInEnum();
    }
}
