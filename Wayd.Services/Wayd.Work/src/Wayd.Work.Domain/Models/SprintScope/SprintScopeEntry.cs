using System.ComponentModel.DataAnnotations;

namespace Wayd.Work.Domain.Models.SprintScope;

/// <summary>How a work item came to be in a sprint's scope.</summary>
public enum SprintScopeEntry
{
    [Display(Name = "Committed", Description = "In the sprint at its effective start.", Order = 1)]
    Committed = 1,

    [Display(Name = "Added", Description = "Entered the sprint after its effective start and before its effective end.", Order = 2)]
    Added = 2,
}
