using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.ProductManagement;

/// <summary>
/// Whether a product stops working without a product it depends on, or carries on without it.
/// </summary>
/// <remarks>
/// Hard and soft as the AWS Well-Architected Framework uses them. Required on every dependency with no
/// default: attributing a provider's downtime to its consumers reads this, and defaulting either way
/// skews that silently — hard over-attributes, soft hides real impact.
/// </remarks>
public enum DependencyStrength
{
    [Display(Name = "Hard", Description = "The product stops working without it.", Order = 1)]
    Hard = 1,

    [Display(Name = "Soft", Description = "The product degrades or loses a feature without it, but keeps working.", Order = 2)]
    Soft = 2
}
