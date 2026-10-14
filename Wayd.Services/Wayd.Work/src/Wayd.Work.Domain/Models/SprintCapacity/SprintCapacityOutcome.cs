using System.ComponentModel.DataAnnotations;

namespace Wayd.Work.Domain.Models.SprintCapacity;

/// <summary>Whether a sprint's scope could be compared with what its capacity is expected to deliver, and if not, why.</summary>
public enum SprintCapacityOutcome
{
    [Display(Name = "Compared", Description = "The scope was compared with what the team's recent sprints delivered for the capacity they had.", Order = 1)]
    Compared = 1,

    [Display(Name = "No capacity", Description = "Nobody is allocated to the team for the sprint's dates in a role that counts toward delivery capacity.", Order = 2)]
    NoCapacity = 2,

    [Display(Name = "No prior sprints", Description = "The team has no completed sprint in its sprint metrics measured in the sprint's sizing method.", Order = 3)]
    NoPriorSprints = 3,

    [Display(Name = "No prior capacity", Description = "Nobody was allocated to the team for its completed sprints, so what it delivers per person-day is unknown.", Order = 4)]
    NoPriorCapacity = 4,
}
