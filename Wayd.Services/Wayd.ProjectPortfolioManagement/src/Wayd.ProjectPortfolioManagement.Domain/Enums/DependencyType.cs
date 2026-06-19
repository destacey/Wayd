using System.ComponentModel.DataAnnotations;

namespace Wayd.ProjectPortfolioManagement.Domain.Enums;

/// <summary>
/// Represents the type of dependency relationship between project tasks.
/// </summary>
public enum DependencyType
{
    /// <summary>
    /// Predecessor task must finish before successor task can start.
    /// </summary>
    [Display(Name = "Finish-to-Start", Description = "Predecessor must finish before successor starts.", Order = 1)]
    FinishToStart = 1

    // Future expansion: FinishToFinish, StartToStart, StartToFinish
}
