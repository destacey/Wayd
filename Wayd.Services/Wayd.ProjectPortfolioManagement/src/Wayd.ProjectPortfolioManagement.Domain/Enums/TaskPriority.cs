using System.ComponentModel.DataAnnotations;

namespace Wayd.ProjectPortfolioManagement.Domain.Enums;

/// <summary>
/// Represents the priority level of a project task.
/// </summary>
public enum TaskPriority
{
    /// <summary>
    /// Low priority task.
    /// </summary>
    [Display(Name = "Low", Description = "Low priority.", Order = 1)]
    Low = 1,

    /// <summary>
    /// Medium priority task.
    /// </summary>
    [Display(Name = "Medium", Description = "Medium priority.", Order = 2)]
    Medium = 2,

    /// <summary>
    /// High priority task.
    /// </summary>
    [Display(Name = "High", Description = "High priority.", Order = 3)]
    High = 3,

    /// <summary>
    /// Critical priority task requiring immediate attention.
    /// </summary>
    [Display(Name = "Critical", Description = "Critical priority requiring immediate attention.", Order = 4)]
    Critical = 4
}
