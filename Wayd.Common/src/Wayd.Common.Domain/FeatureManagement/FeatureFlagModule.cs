using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.FeatureManagement;

/// <summary>
/// The area of the app a feature flag belongs to. The Feature Flags page groups flags by it, in
/// <see cref="DisplayAttribute.Order"/>. Stored by name, so values can be reordered but never renamed or
/// removed: an archived flag keeps its last module, and a stored name the enum no longer has fails to load.
/// </summary>
public enum FeatureFlagModule
{
    [Display(Name = "Platform", Description = "Capabilities that work across the whole app.", Order = 1)]
    Platform = 1,

    [Display(Name = "Planning", Description = "Planning intervals, estimation and forecasting.", Order = 2)]
    Planning = 2,

    [Display(Name = "Organization", Description = "Teams, employees and capacity.", Order = 3)]
    Organization = 3,

    [Display(Name = "Product Management", Description = "Product catalog, releases and incidents.", Order = 4)]
    ProductManagement = 4,

    [Display(Name = "Demand Management", Description = "Intake requests and the desks that review them.", Order = 5)]
    DemandManagement = 5,
}
