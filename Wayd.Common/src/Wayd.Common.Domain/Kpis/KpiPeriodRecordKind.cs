using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Kpis;

/// <summary>The kind of record a measurement's period covers, when it covers one.</summary>
public enum KpiPeriodRecordKind
{
    [Display(Name = "Sprint", Description = "The period is a sprint.", Order = 1)]
    Sprint = 1,

    [Display(Name = "Planning Interval", Description = "The period is a planning interval.", Order = 2)]
    PlanningInterval = 2,

    [Display(Name = "Release", Description = "The period is a release.", Order = 3)]
    Release = 3
}
