using System.ComponentModel.DataAnnotations;
using Wayd.Common.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Domain.Enums;

/// <summary>
/// Represents the status of a program in its lifecycle.
/// </summary>
public enum ProgramStatus
{
    [Display(Name = "Proposed", Description = "The program is in the initial planning or conceptualization stage. It has not yet been approved or activated.", Order = 1, GroupName = nameof(LifecycleCategory.NotStarted))]
    Proposed = 1,

    [Display(Name = "Active", Description = "The program has been approved and is actively managed, with projects in progress.", Order = 2, GroupName = nameof(LifecycleCategory.Active))]
    Active = 2,

    [Display(Name = "Completed", Description = "The program has achieved its goals and is considered finished.", Order = 3, GroupName = nameof(LifecycleCategory.Completed))]
    Completed = 3,

    [Display(Name = "Cancelled", Description = "The program was terminated before achieving its goals.", Order = 4, GroupName = nameof(LifecycleCategory.Completed))]
    Cancelled = 4
}
