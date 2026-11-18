using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Kpis;

/// <summary>How a KPI's optional at-risk threshold is stated.</summary>
public enum KpiAtRiskKind
{
    [Display(Name = "Absolute", Description = "The threshold is a value: a measurement past it, but short of the target, is at risk.", Order = 1)]
    Absolute = 1,

    [Display(Name = "Progress to Target", Description = "The threshold is a percentage of the way from the baseline to the target.", Order = 2)]
    ProgressToTarget = 2
}
