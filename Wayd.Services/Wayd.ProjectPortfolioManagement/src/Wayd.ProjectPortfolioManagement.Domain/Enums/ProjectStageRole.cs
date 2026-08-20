using System.ComponentModel.DataAnnotations;

namespace Wayd.ProjectPortfolioManagement.Domain.Enums;

/// <summary>
/// Represents the role an employee can have for a project phase.
/// </summary>
public enum ProjectPhaseRole
{
    /// <summary>
    /// Responsible for completing the phase.
    /// </summary>
    [Display(Name = "Assignee", Description = "Responsible for completing the phase.", Order = 1)]
    Assignee = 1,
}
