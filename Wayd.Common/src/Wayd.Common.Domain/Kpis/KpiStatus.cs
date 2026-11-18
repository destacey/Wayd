using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Kpis;

/// <summary>Whether a KPI is still being measured.</summary>
public enum KpiStatus
{
    [Display(Name = "Active", Description = "The KPI is on its subject's scorecard and takes measurements.", Order = 1)]
    Active = 1,

    [Display(Name = "Closed", Description = "The KPI has left the scorecard and keeps its measurements as history.", Order = 2)]
    Closed = 2
}
