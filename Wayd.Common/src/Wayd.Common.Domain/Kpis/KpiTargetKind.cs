using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Kpis;

/// <summary>How a KPI's target is stated.</summary>
/// <remarks>"Reach 15 days" and "improve 25%" can be the same target entered two ways.</remarks>
public enum KpiTargetKind
{
    [Display(Name = "Absolute", Description = "The target is a value to reach.", Order = 1)]
    Absolute = 1,

    [Display(Name = "Relative Change", Description = "The target is a percentage change from the baseline, in the KPI's direction.", Order = 2)]
    RelativeChange = 2
}
