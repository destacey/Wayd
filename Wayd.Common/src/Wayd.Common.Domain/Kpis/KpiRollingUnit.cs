using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Kpis;

/// <summary>What a rolling KPI counts its window in.</summary>
public enum KpiRollingUnit
{
    [Display(Name = "Days", Description = "The window is a number of calendar days ending on the day the value is taken.", Order = 1)]
    Days = 1
}
