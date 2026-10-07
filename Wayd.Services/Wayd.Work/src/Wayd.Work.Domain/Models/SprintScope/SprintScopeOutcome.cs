using System.ComponentModel.DataAnnotations;

namespace Wayd.Work.Domain.Models.SprintScope;

/// <summary>What became of a work item that was in a sprint's scope.</summary>
public enum SprintScopeOutcome
{
    [Display(Name = "Completed", Description = "In a Done-category status at the sprint's effective end, or when it left the sprint.", Order = 1)]
    Completed = 1,

    [Display(Name = "Completed as Removed", Description = "Reached a Removed-category status while in the sprint. It counts as completed, and the team gets credit.", Order = 2)]
    Removed = 2,

    [Display(Name = "Carried Over", Description = "Unfinished, and still in the sprint at its effective end or moved to the team's next sprint on its last day.", Order = 3)]
    CarriedOver = 3,

    [Display(Name = "Descoped", Description = "Unfinished, and left the sprint before its last day, or on it for somewhere other than the team's next sprint.", Order = 4)]
    Descoped = 4,

    [Display(Name = "Remaining", Description = "Unfinished, and still in a sprint that has not ended yet.", Order = 5)]
    Remaining = 5,
}
