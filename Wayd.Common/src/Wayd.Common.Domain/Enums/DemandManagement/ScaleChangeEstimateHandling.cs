using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.DemandManagement;

/// <summary>
/// What happens to the estimates on a desk's open requests when the desk changes its estimation scale or version.
/// </summary>
/// <remarks>
/// Decided once per change, for every open request on the desk. Done and Removed requests keep their estimates
/// either way.
/// </remarks>
public enum ScaleChangeEstimateHandling
{
    /// <summary>
    /// Each open request keeps its estimate on the version it was given on until it is re-estimated.
    /// </summary>
    [Display(Name = "Keep", Description = "Open requests keep their estimates on the version they were given on until re-estimated.", Order = 1)]
    Keep = 1,

    /// <summary>
    /// Every estimate on an open request is cleared.
    /// </summary>
    [Display(Name = "Clear", Description = "Every estimate on an open request is cleared.", Order = 2)]
    Clear = 2,
}
