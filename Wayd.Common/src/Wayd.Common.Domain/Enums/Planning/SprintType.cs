using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.Planning;

/// <summary>
/// Whether a sprint is comparable with the team's other sprints. Non-standard marks a sprint to leave out when
/// comparing the team's sprints, such as averaging velocity; the sprint's own metrics still show.
/// </summary>
/// <remarks>
/// Stored by name, so a member may be added or reordered but never renamed. Max length of 32 characters.
/// </remarks>
public enum SprintType
{
    [Display(Name = "Standard", Description = "A regular sprint, comparable with the team's other sprints.", Order = 1)]
    Standard = 1,

    [Display(Name = "Non-standard", Description = "A sprint that isn't comparable with the team's other sprints, such as an innovation and planning sprint, a hackathon or a holiday period. Its own metrics still show, but it is marked to be left out when comparing the team's sprints.", Order = 2)]
    NonStandard = 2,
}
