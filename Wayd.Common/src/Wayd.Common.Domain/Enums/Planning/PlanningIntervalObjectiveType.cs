using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.Planning;

// max length of 32 characters
public enum PlanningIntervalObjectiveType
{
    [Display(Name = "Team", Order = 1)]
    Team = 1,
    [Display(Name = "Team of Teams", Order = 2)]
    TeamOfTeams = 2,
}
