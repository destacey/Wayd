using System.ComponentModel.DataAnnotations;
using Wayd.Common.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Domain.Enums;

/// <summary>
/// Represents the status of a project task.
/// </summary>
public enum TaskStatus
{
    /// <summary>
    /// The task has not been started.
    /// </summary>
    [Display(Name = "Not Started", Description = "The task has not been started.", Order = 1, GroupName = nameof(LifecyclePhase.NotStarted))]
    NotStarted = 1,

    /// <summary>
    /// The task is currently in progress.
    /// </summary>
    [Display(Name = "In Progress", Description = "The task is currently being worked on.", Order = 2, GroupName = nameof(LifecyclePhase.Active))]
    InProgress = 2,

    /// <summary>
    /// The task has been completed.
    /// </summary>
    [Display(Name = "Completed", Description = "The task has been completed.", Order = 3, GroupName = nameof(LifecyclePhase.NotStarted))]
    Completed = 3,

    /// <summary>
    /// The task has been cancelled and will not be completed.
    /// </summary>
    [Display(Name = "Cancelled", Description = "The task has been cancelled.", Order = 4, GroupName = nameof(LifecyclePhase.NotStarted))]
    Cancelled = 4
}
