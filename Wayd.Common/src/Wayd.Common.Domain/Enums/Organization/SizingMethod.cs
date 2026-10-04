using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.Organization;

/// <summary>
/// Defines the sizing method a team uses to estimate work items: which of a work item's estimates the team's
/// metrics read. An item with no value in that estimate is unestimated; another estimate never stands in.
/// </summary>
public enum SizingMethod
{
    /// <summary>
    /// Relative sizing using the work item's story points.
    /// </summary>
    [Display(Name = "Story Points", Description = "Relative sizing using story points", Order = 1)]
    StoryPoints = 1,

    /// <summary>
    /// Item count where each item equals 1.
    /// </summary>
    [Display(Name = "Count", Description = "Item count (each item = 1)", Order = 2)]
    Count = 2,

    /// <summary>
    /// Sizing using the work item's effort, as the Scrum process estimates its backlog items.
    /// </summary>
    [Display(Name = "Effort", Description = "Sizing using effort", Order = 3)]
    Effort = 3,

    /// <summary>
    /// Sizing using the work item's size, as the CMMI process estimates its requirements.
    /// </summary>
    [Display(Name = "Size", Description = "Sizing using size", Order = 4)]
    Size = 4,
}
