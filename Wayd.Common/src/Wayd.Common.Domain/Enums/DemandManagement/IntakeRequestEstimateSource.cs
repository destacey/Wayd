using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.DemandManagement;

/// <summary>
/// How an intake request's estimate came to change.
/// </summary>
public enum IntakeRequestEstimateSource
{
    /// <summary>
    /// A member of the desk team set, changed or cleared it.
    /// </summary>
    [Display(Name = "Manual", Description = "Set, changed or cleared by the desk team.", Order = 1)]
    Manual = 1,

    /// <summary>
    /// The request moved to a desk estimating on another scale or version, or on none, which cleared it.
    /// </summary>
    [Display(Name = "Rerouted", Description = "Cleared when the request moved to a desk estimating on another scale.", Order = 2)]
    Rerouted = 2,

    /// <summary>
    /// An import brought it over from the tool the request was sized in.
    /// </summary>
    [Display(Name = "Imported", Description = "Brought over by an import.", Order = 3)]
    Imported = 3,

    /// <summary>
    /// The request's desk changed its estimation scale or version and chose to clear the estimates on its open requests.
    /// </summary>
    [Display(Name = "Scale Change", Description = "Cleared when the desk changed its estimation scale.", Order = 4)]
    ScaleChange = 4,
}
