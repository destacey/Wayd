using System.ComponentModel.DataAnnotations;

namespace Wayd.Work.Application.WorkItems.Dtos;

public enum ForecastIssueType
{
    [Display(Name = "No Team", Description = "The work item is not assigned to a team, so it has no backlog position or throughput.", Order = 1)]
    NoTeam = 1,

    [Display(Name = "Not a Backlog Item", Description = "Only requirement-tier work items (stories, bugs) have a backlog position.", Order = 2)]
    NotABacklogItem = 2,

    [Display(Name = "Not Enough History", Description = "The work item's team has not finished enough work recently to forecast from.", Order = 3)]
    NotEnoughHistory = 3,
}
