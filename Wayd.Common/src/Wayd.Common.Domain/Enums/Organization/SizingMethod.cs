using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.Organization;

/// <summary>
/// Defines the sizing method a team uses to estimate work items.
/// </summary>
public enum SizingMethod
{
    /// <summary>
    /// Relative sizing using story points.
    /// </summary>
    [Display(Name = "Story Points", Description = "Relative sizing using story points", Order = 1)]
    StoryPoints = 1,

    /// <summary>
    /// Item count where each item equals 1.
    /// </summary>
    [Display(Name = "Count", Description = "Item count (each item = 1)", Order = 2)]
    Count = 2
}
