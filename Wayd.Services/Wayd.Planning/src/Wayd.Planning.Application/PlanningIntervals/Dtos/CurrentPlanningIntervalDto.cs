using Wayd.Common.Application.Dtos;

namespace Wayd.Planning.Application.PlanningIntervals.Dtos;

/// <summary>The planning interval running today for a group of teams, and how many of their objectives in it are in trouble.</summary>
public sealed record CurrentPlanningIntervalDto
{
    /// <summary>The planning interval.</summary>
    public required NavigationDto PlanningInterval { get; init; }

    /// <summary>
    /// The teams' objectives in it, not yet completed, canceled or missed, whose current health check is At Risk
    /// or Unhealthy. Null when they were not counted.
    /// </summary>
    public int? ObjectivesAtRiskCount { get; init; }
}
