using Wayd.Common.Application.Dtos;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Web.Api.Models.Planning.PlanningIntervals;

/// <summary>
/// Response containing PI Iteration metrics aggregated across all mapped sprints.
/// </summary>
public sealed record PlanningIntervalIterationMetricsResponse
{
    public Guid IterationId { get; init; }
    public int IterationKey { get; init; }
    public required string IterationName { get; init; }
    public required LocalDate Start { get; init; }
    public required LocalDate End { get; init; }
    public required SimpleNavigationDto Category { get; init; }

    // Aggregated metrics
    public int TeamCount { get; init; }
    public int SprintCount { get; init; }

    public int TotalWorkItems { get; init; }
    public double TotalStoryPoints { get; init; }

    public int CompletedWorkItems { get; init; }
    public double CompletedStoryPoints { get; init; }

    public int InProgressWorkItems { get; init; }
    public double InProgressStoryPoints { get; init; }

    public int NotStartedWorkItems { get; init; }
    public double NotStartedStoryPoints { get; init; }

    public int MissingStoryPointsCount { get; init; }

    /// <summary>Cycle-time rollup across all sprints in this iteration.</summary>
    public required CycleTimeSummary CycleTime { get; init; }

    // Per-sprint breakdown
    public required IReadOnlyList<SprintMetricsSummary> SprintMetrics { get; init; }
}

/// <summary>
/// Metrics summary for an individual sprint within the PI Iteration.
/// </summary>
public sealed record SprintMetricsSummary
{
    public Guid SprintId { get; init; }
    public int SprintKey { get; init; }
    public required string SprintName { get; init; }
    public required SimpleNavigationDto State { get; init; }
    public LocalDate Start { get; init; }
    public LocalDate End { get; init; }
    public required NavigationDto Team { get; init; }

    // Metrics
    public int TotalWorkItems { get; init; }
    public double TotalStoryPoints { get; init; }
    public int CompletedWorkItems { get; init; }
    public double CompletedStoryPoints { get; init; }
    public int InProgressWorkItems { get; init; }
    public double InProgressStoryPoints { get; init; }
    public int NotStartedWorkItems { get; init; }
    public double NotStartedStoryPoints { get; init; }
    public int MissingStoryPointsCount { get; init; }

    /// <summary>Cycle-time rollup for this sprint.</summary>
    public required CycleTimeSummary CycleTime { get; init; }
}
