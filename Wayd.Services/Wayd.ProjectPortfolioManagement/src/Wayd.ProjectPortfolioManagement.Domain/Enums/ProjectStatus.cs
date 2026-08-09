using System.ComponentModel.DataAnnotations;
using Wayd.Common.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Domain.Enums;

// max length of 32 characters

/// <summary>
/// Represents the various statuses a project can have during its lifecycle.
/// </summary>
public enum ProjectStatus
{
    [Display(Name = "Proposed", Description = "The project is in the initial planning and evaluation stage.", Order = 1, GroupName = nameof(LifecycleCategory.NotStarted))]
    Proposed = 1,

    [Display(Name = "Approved", Description = "The project has been reviewed and approved to proceed.", Order = 2, GroupName = nameof(LifecycleCategory.NotStarted))]
    Approved = 5,

    [Display(Name = "Active", Description = "The project has been approved and is currently in progress.", Order = 3, GroupName = nameof(LifecycleCategory.Active))]
    Active = 2,

    [Display(Name = "Completed", Description = "The project has been successfully completed and closed.", Order = 4, GroupName = nameof(LifecycleCategory.Completed))]
    Completed = 3,

    [Display(Name = "Canceled", Description = "The project has been terminated before completion.", Order = 5, GroupName = nameof(LifecycleCategory.Canceled))]
    Canceled = 4
}
