using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.Planning;

// max length of 32 characters
public enum RoadmapState
{
    [Display(Name = "Active", Description = "The roadmap is currently in use.", Order = 1)]
    Active = 1,

    [Display(Name = "Archived", Description = "The roadmap is no longer active but retained for historical purposes.", Order = 2)]
    Archived = 2
}
