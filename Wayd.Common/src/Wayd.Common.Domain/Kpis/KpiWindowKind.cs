using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Kpis;

/// <summary>How a KPI's measurements are windowed: which span of time each value describes.</summary>
public enum KpiWindowKind
{
    [Display(Name = "Per Period", Description = "Each measurement gives its own period, and periods do not overlap: a value per sprint, week or month.", Order = 1)]
    PerPeriod = 1,

    [Display(Name = "Cumulative", Description = "Each measurement covers the days from a fixed anchor date to the day it is taken: a value since a start date.", Order = 2)]
    Cumulative = 2,

    [Display(Name = "Rolling", Description = "Each measurement covers a fixed number of units ending on the day it is taken: a value over the last 90 days.", Order = 3)]
    Rolling = 3,

    [Display(Name = "Point in Time", Description = "Each measurement is the state at one moment, such as open bugs or work in progress; several can be taken a day.", Order = 4)]
    PointInTime = 4
}
