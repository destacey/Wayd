using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.Planning;

/// <summary>
/// Whether a sprint is comparable with the team's other sprints. Rollups across sprints, such as average
/// velocity, leave a non-standard sprint out; the sprint's own metrics still show.
/// </summary>
/// <remarks>
/// Stored by name, so a member may be added or reordered but never renamed. Max length of 32 characters.
/// </remarks>
public enum SprintType
{
    [Display(Name = "Standard", Description = "A regular sprint, comparable with the team's other sprints and counted in rollups across them.", Order = 1)]
    Standard = 1,

    [Display(Name = "Non-standard", Description = "A sprint that isn't comparable with the team's other sprints, such as an innovation and planning sprint, a hackathon or a holiday period. Its own metrics still show, but rollups across sprints leave it out.", Order = 2)]
    NonStandard = 2,
}
