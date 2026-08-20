using System.ComponentModel.DataAnnotations;

namespace Wayd.ProjectPortfolioManagement.Domain.Enums;

/// <summary>
/// Represents the role an employee can have for a project stage.
/// </summary>
public enum ProjectStageRole
{
    /// <summary>
    /// Responsible for completing the stage.
    /// </summary>
    [Display(Name = "Assignee", Description = "Responsible for completing the stage.", Order = 1)]
    Assignee = 1,
}
