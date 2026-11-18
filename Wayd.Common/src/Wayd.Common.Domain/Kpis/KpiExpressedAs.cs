using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Kpis;

/// <summary>Which reading of a KPI's raw value it is shown as. A reading, never stored itself.</summary>
public enum KpiExpressedAs
{
    [Display(Name = "Value", Description = "The measured value as it was recorded.", Order = 1)]
    Value = 1,

    [Display(Name = "Change from Baseline", Description = "The percentage change of the measured value from the baseline.", Order = 2)]
    ChangeFromBaseline = 2,

    [Display(Name = "Progress to Target", Description = "How far the measured value has moved from the baseline toward the target, as a percentage.", Order = 3)]
    ProgressToTarget = 3
}
