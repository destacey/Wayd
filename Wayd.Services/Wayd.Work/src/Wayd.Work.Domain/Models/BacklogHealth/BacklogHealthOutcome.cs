using System.ComponentModel.DataAnnotations;

namespace Wayd.Work.Domain.Models.BacklogHealth;

public enum BacklogHealthOutcome
{
    [Display(Name = "Assessed", Description = "The check was graded.", Order = 1)]
    Assessed = 1,

    [Display(Name = "Not Enough History", Description = "The team completed too few work items in the lookback window to grade the check.", Order = 2)]
    NotEnoughHistory = 2,

    [Display(Name = "Not Applicable", Description = "No work items fall within the check's scope.", Order = 3)]
    NotApplicable = 3,
}
