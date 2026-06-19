using System.ComponentModel.DataAnnotations;

namespace Wayd.ProjectPortfolioManagement.Domain.Enums;

/// <summary>
/// Represents the type of a project task.
/// </summary>
public enum ProjectTaskType
{
    /// <summary>
    /// A regular work task with start and end dates that can have child tasks.
    /// </summary>
    [Display(Name = "Task", Description = "A regular work task with start and end dates.", Order = 1)]
    Task = 1,

    /// <summary>
    /// A single-date marker representing a significant point or deliverable. Cannot have children.
    /// </summary>
    [Display(Name = "Milestone", Description = "A single-date marker representing a significant point.", Order = 2)]
    Milestone = 2
}
