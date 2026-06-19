using Wayd.Common.Application.Dtos;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Web.Api.Models.Planning.PlanningIntervals;

/// <summary>
/// Response containing PI-level metrics aggregated across all mapped sprints in
/// every iteration of the Planning Interval. Intended as a thin, extensible
/// rollup; per-iteration breakdowns remain on the iteration-level endpoint.
/// </summary>
public sealed record PlanningIntervalMetricsResponse
{
    public Guid PlanningIntervalId { get; init; }
    public int PlanningIntervalKey { get; init; }
    public required string PlanningIntervalName { get; init; }

    public int TeamCount { get; init; }
    public int SprintCount { get; init; }

    /// <summary>Cycle-time rollup across every sprint in every iteration of this PI.</summary>
    public required CycleTimeSummary CycleTime { get; init; }

    public required IReadOnlyList<PlanningIntervalTeamMetrics> TeamMetrics { get; init; }
}

/// <summary>
/// Per-team rollup for a Planning Interval. Cycle time is averaged across all
/// of the team's sprints mapped to any iteration in the PI; predictability
/// matches the value returned by the predictability endpoint.
/// </summary>
public sealed record PlanningIntervalTeamMetrics
{
    public required NavigationDto Team { get; init; }

    /// <summary>The team's short code (e.g. "CORE"). Used to route to the
    /// team-specific tab on the plan-review page.</summary>
    public required string TeamCode { get; init; }

    public double? Predictability { get; init; }

    /// <summary>Count of non-stretch (committed) objectives for this team in the PI.</summary>
    public int RegularObjectivesCount { get; init; }
    public int StretchObjectivesCount { get; init; }
    public int CompletedObjectivesCount { get; init; }

    public required CycleTimeSummary CycleTime { get; init; }
    public int SprintCount { get; init; }
}
