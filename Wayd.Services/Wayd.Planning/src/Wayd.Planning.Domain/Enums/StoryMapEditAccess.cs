using System.ComponentModel.DataAnnotations;

namespace Wayd.Planning.Domain.Enums;

/// <summary>
/// Who can change a public story map's content. A private map admits only its owners and editors, so there it has
/// no effect. Stored as text; max length of 32 characters.
/// </summary>
public enum StoryMapEditAccess
{
    [Display(Name = "Anyone who can view", Description = "Anyone who can see the map can change its content.", Order = 1)]
    AnyoneWhoCanView = 1,

    [Display(Name = "Owners and editors only", Description = "Only the map's owners and editors can change its content.", Order = 2)]
    OwnersAndEditors = 2,
}
