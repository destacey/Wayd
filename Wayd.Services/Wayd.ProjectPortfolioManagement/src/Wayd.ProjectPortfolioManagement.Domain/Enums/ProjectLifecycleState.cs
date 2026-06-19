using System.ComponentModel.DataAnnotations;

namespace Wayd.ProjectPortfolioManagement.Domain.Enums;

// max length of 32 characters

public enum ProjectLifecycleState
{
    [Display(Name = "Proposed", Description = "The lifecycle is defined but not yet approved for use.", Order = 1)]
    Proposed = 1,

    [Display(Name = "Active", Description = "The lifecycle is approved and available for project assignment.", Order = 2)]
    Active = 2,

    [Display(Name = "Archived", Description = "The lifecycle is no longer available for new projects but remains for historical reference.", Order = 3)]
    Archived = 3
}
