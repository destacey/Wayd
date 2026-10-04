using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.Planning;

public enum RoadmapItemType
{
    [Display(Name = "Activity", Description = "An activity is a core component of the roadmap, representing a theme, project, piece of work or a group of related tasks.", Order = 1)]
    Activity = 1,

    [Display(Name = "Milestone", Description = "A milestone represents significant points or achievements within a roadmap or roadmap item.", Order = 2)]
    Milestone = 2,

    [Display(Name = "Timebox", Description = "A timebox typically represents a fixed time period that can span across multiple activities or milestones.", Order = 3)]
    Timebox = 3
}
