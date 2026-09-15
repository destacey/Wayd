using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.Planning;

// max length of 32 characters
public enum ObjectiveStatus
{
    [Display(Name = "Not Started", Description = "The objective has not been started.", Order = 1)]
    NotStarted = 1,

    [Display(Name = "In Progress", Description = "The objective is in progress.", Order = 2)]
    InProgress = 2,

    [Display(Name = "Completed", Description = "The objective has been completed.", Order = 3)]
    Completed = 3,

    [Display(Name = "Canceled", Description = "The objective was canceled.", Order = 4)]
    Canceled = 4,

    [Display(Name = "Missed", Description = "The objective was missed.", Order = 5)]
    Missed = 5,
}
