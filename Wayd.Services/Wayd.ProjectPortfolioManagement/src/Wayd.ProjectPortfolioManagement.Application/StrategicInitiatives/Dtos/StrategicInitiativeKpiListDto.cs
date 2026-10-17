using Wayd.Common.Domain.Models.KeyPerformanceIndicators;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Dtos;

public sealed record StrategicInitiativeKpiListDto : IMapFrom<StrategicInitiativeKpi>
{
    public Guid Id { get; set; }

    /// <summary>
    /// The KPI's unique key, an alternate to its <c>id</c>. Endpoints differ in which of the two they accept; a uuid parameter takes the id.
    /// </summary>
    public int Key { get; set; }

    /// <summary>
    /// The name of the KPI.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The starting (baseline) value of the KPI. Used to track progress relative to where the KPI began.
    /// </summary>
    public double? StartingValue { get; set; }

    /// <summary>
    /// The target value that defines success for the KPI.
    /// </summary>
    public double TargetValue { get; set; }

    /// <summary>
    /// The actual value of the KPI.
    /// </summary>
    public double? ActualValue { get; set; }

    /// <summary>
    /// The progress towards the KPI target, calculated based on the starting value, actual value, target value, and target direction.
    /// </summary>
    public double? Progress => KpiUtils.CalculateProgress(StartingValue, ActualValue, TargetValue, TargetDirection);

    /// <summary>
    /// An optional prefix symbol displayed before the numeric value (e.g. "$").
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// An optional suffix symbol displayed after the numeric value (e.g. "%", "M").
    /// </summary>
    public string? Suffix { get; set; }

    /// <summary>
    /// Whether the KPI succeeds by increasing or by decreasing toward its target value.
    /// </summary>
    public KpiTargetDirection TargetDirection { get; set; }

    /// <summary>
    /// The display order of the KPI within its strategic initiative. Lower values appear first.
    /// </summary>
    public int Order { get; set; }
}
