using System.ComponentModel.DataAnnotations;
using Wayd.Common.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Domain.Enums;

// max length of 32 characters

public enum ProjectPortfolioStatus
{
    [Display(Name = "Proposed", Description = "The portfolio is in the early planning or conceptualization stage. It has not yet been approved or activated for execution.", Order = 1, GroupName = nameof(LifecycleCategory.NotStarted))]
    Proposed = 1,

    [Display(Name = "Active", Description = "The portfolio has been approved and is actively managed, with projects/programs in progress.", Order = 2, GroupName = nameof(LifecycleCategory.Active))]
    Active = 2,

    [Display(Name = "On Hold", Description = "The portfolio is temporarily paused, and active work on associated projects/programs is halted.", Order = 3, GroupName = nameof(LifecycleCategory.Active))]
    OnHold = 3,

    [Display(Name = "Closed", Description = "The project portfolio has been successfully executed. All associated projects/programs must be completed or canceled.", Order = 4, GroupName = nameof(LifecycleCategory.Completed))]
    Closed = 4,

    [Display(Name = "Archived", Description = "The project portfolio is no longer active but retained for historical purposes.", Order = 5, GroupName = nameof(LifecycleCategory.Completed))]
    Archived = 5
}
