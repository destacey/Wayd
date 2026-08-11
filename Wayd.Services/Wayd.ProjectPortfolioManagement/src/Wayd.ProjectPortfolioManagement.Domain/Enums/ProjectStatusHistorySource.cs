using System.ComponentModel.DataAnnotations;

namespace Wayd.ProjectPortfolioManagement.Domain.Enums;

/// <summary>
/// How a project status history row came to exist, so a reconstructed row does not claim the fidelity
/// of one written as the transition happened.
/// </summary>
public enum ProjectStatusHistorySource
{
    [Display(Name = "Recorded", Description = "Written by the project as the transition occurred.", Order = 1)]
    Recorded = 1,

    [Display(Name = "Reconstructed", Description = "Derived from an audit trail entry for a transition that predates status history.", Order = 2)]
    Reconstructed = 2,

    [Display(Name = "Synthesized", Description = "Inferred to give a project a starting point where no audit trail recorded its status.", Order = 3)]
    Synthesized = 3
}
