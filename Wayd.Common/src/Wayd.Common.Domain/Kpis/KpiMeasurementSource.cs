using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Kpis;

/// <summary>How a measurement's value was arrived at.</summary>
public enum KpiMeasurementSource
{
    [Display(Name = "Manual", Description = "The value was entered by hand.", Order = 1)]
    Manual = 1
}
