using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.Work;

// max length of 32 characters
public enum DependencyPlanningHealth
{
    [Display(Name = "Healthy", Description = "The predecessor is planned before the successor.", Order = 1)]
    Healthy = 1,

    [Display(Name = "At Risk", Description = "The predecessor is planned at the same time as the successor or neither side is planned.", Order = 2)]
    AtRisk = 2,

    [Display(Name = "Unhealthy", Description = "The predecessor is planned after the successor.", Order = 3)]
    Unhealthy = 3,

    [Display(Name = "Unknown", Description = "The planning health cannot be determined.", Order = 4)]
    Unknown = 4,

    [Display(Name = "Closed", Description = "The predecessor is done, or it was removed and the successor is done or removed too. Planning no longer applies.", Order = 5)]
    Closed = 5
}
