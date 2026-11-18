using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Kpis;

/// <summary>Where a KPI's measurements come from.</summary>
/// <remarks>A source belongs to the KPI rather than to each measurement's history, so automating a KPI switches its source and keeps the values already recorded.</remarks>
public enum KpiSource
{
    [Display(Name = "Manual", Description = "Measurements are entered by hand.", Order = 1)]
    Manual = 1
}
