using Wayd.Common.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;

/// <summary>
/// Represents the parameters required to create or update a strategic initiative KPI.
/// </summary>
/// <param name="Name">The name of the KPI.</param>
/// <param name="Description">A description of what the KPI measures.</param>
/// <param name="StartingValue">The starting (baseline) value of the KPI.</param>
/// <param name="TargetValue">The target value that defines success for the KPI.</param>
/// <param name="Prefix">An optional prefix symbol displayed before the numeric value (e.g. "$").</param>
/// <param name="Suffix">An optional suffix symbol displayed after the numeric value (e.g. "%", "M").</param>
/// <param name="TargetDirection">The target direction for the KPI.</param>
public sealed record StrategicInitiativeKpiUpsertParameters(string Name, string? Description, double? StartingValue, double TargetValue, string? Prefix, string? Suffix, KpiTargetDirection TargetDirection);

