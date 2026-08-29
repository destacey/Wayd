using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.ProductManagement;

/// <summary>
/// What kind of target an environment is. Pipeline environment <em>names</em> are free text and
/// endlessly varied — prod, Production, prd, live, somebody's prod-canary — so the category is what
/// every measure scoped to production actually counts on.
/// </summary>
/// <remarks>
/// This is a fixed enum rather than a workflow, because it is a classification rather than a state: an
/// environment does not move through these, and deployment frequency has no denominator until each
/// environment maps to one. Reclassifying an environment retroactively changes every production-scoped
/// measure, which is why it raises its own event rather than passing as an ordinary edit.
/// </remarks>
public enum EnvironmentCategory
{
    [Display(Name = "Development", Description = "Engineering environments used while building.", Order = 1)]
    Development = 1,

    [Display(Name = "Testing", Description = "Environments used to verify a change before release.", Order = 2)]
    Testing = 2,

    [Display(Name = "Staging", Description = "Production-like environments used for final validation.", Order = 3)]
    Staging = 3,

    [Display(Name = "Production", Description = "Live environments serving real users. The denominator for delivery metrics.", Order = 4)]
    Production = 4
}
