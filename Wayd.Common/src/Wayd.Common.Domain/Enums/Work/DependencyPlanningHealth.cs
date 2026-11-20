using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.Work;

// max length of 32 characters
public enum DependencyPlanningHealth
{
    [Display(Name = "Healthy", Description = "The successor has no planned dates, or the predecessor is planned to finish by the day the successor is planned to start.", Order = 1)]
    Healthy = 1,

    [Display(Name = "At Risk", Description = "The predecessor is planned to finish after the successor is planned to start but by the day it is planned to finish, or the predecessor has started without planned dates while the successor has them.", Order = 2)]
    AtRisk = 2,

    [Display(Name = "Unhealthy", Description = "The predecessor is planned to finish after the successor, or has neither started nor planned dates while the successor has them; the successor has started, finished or been removed while the predecessor is still open; or the predecessor was removed while the successor is still open.", Order = 3)]
    Unhealthy = 3,

    [Display(Name = "Unknown", Description = "The planning health cannot be determined.", Order = 4)]
    Unknown = 4,

    [Display(Name = "Closed", Description = "The predecessor is done, or it was removed and the successor is done or removed too. Planning no longer applies.", Order = 5)]
    Closed = 5
}
