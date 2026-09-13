using Wayd.Common.Domain.Models.KeyPerformanceIndicators;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A KPI's descriptive fields taken together, as <see cref="StrategicInitiativeKpiDetailsUpdatedEvent.Previous"/>
/// records the values an edit replaced.
/// </summary>
public sealed record StrategicInitiativeKpiDetails(string Name, string? Description, string? Prefix, string? Suffix);

/// <summary>
/// What a KPI is measured against, as <see cref="StrategicInitiativeKpiTargetChangedEvent.Previous"/> records
/// the values a change replaced.
/// </summary>
public sealed record StrategicInitiativeKpiTarget(double? StartingValue, double TargetValue, KpiTargetDirection TargetDirection);

/// <summary>
/// One checkpoint in a KPI's plan, as carried by <see cref="StrategicInitiativeKpiCheckpointPlanChangedEvent"/>.
/// </summary>
public sealed record StrategicInitiativeKpiCheckpointValues(
    Guid CheckpointId,
    double TargetValue,
    double? AtRiskValue,
    Instant CheckpointDate,
    string DateLabel);

/// <summary>
/// A checkpoint that stayed in the plan with different values: both ends of the change.
/// </summary>
public sealed record StrategicInitiativeKpiCheckpointRevision(
    StrategicInitiativeKpiCheckpointValues Previous,
    StrategicInitiativeKpiCheckpointValues Current);
