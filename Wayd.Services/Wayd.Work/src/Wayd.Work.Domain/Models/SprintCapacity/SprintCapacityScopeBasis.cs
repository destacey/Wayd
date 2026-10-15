using System.ComponentModel.DataAnnotations;

namespace Wayd.Work.Domain.Models.SprintCapacity;

/// <summary>Which of a sprint's scope is set against its capacity.</summary>
public enum SprintCapacityScopeBasis
{
    [Display(Name = "Planned", Description = "The work in the sprint now, before its commitment point.", Order = 1)]
    Planned = 1,

    [Display(Name = "Committed", Description = "The work in the sprint at its commitment point, at the estimates it had then.", Order = 2)]
    Committed = 2,
}
