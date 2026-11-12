using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums;

public enum SystemContext
{
    // Current Db length is set to 64 characters
    // Template: [Service].[Domain/Aggregate?].[Object]

    // 0 - 999: Common


    // 1000 - 1999: Organization


    // 2000 - 2999: Planning
    [Display(Name = "Planning.PlanningInterval.Objective", Description = "Planning Interval Objective", Order = 1)]
    PlanningPlanningIntervalObjective = 2010,

    // 3000 - 3999: Work
    [Display(Name = "Work.WorkProcess", Description = "Work Process", Order = 2)]
    WorkWorkProcess = 3010,

    // 4000 - 4999: Product Management
    [Display(Name = "ProductManagement.Incident", Description = "Incident", Order = 3)]
    ProductManagementIncident = 4010,
}
