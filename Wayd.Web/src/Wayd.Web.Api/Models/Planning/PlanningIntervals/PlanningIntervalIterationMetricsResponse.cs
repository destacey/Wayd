using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.Organization;
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

    /// <summary>
    /// The sizing method every sprint in the iteration is measured in, which the estimate totals are in. Null
    /// when the sprints use different sizing methods, or there are none: estimates in different units are never
    /// added, so the estimate totals are null too and only the counts roll up.
    /// </summary>
    public SizingMethod? SizingMethod { get; init; }

    public int TotalWorkItems { get; init; }
    public double? TotalEstimate { get; init; }

    public int CompletedWorkItems { get; init; }
    public double? CompletedEstimate { get; init; }

    public int InProgressWorkItems { get; init; }
    public double? InProgressEstimate { get; init; }

    public int NotStartedWorkItems { get; init; }
    public double? NotStartedEstimate { get; init; }

    /// <summary>Items with no value in their own sprint's sizing method, across every sprint.</summary>
    public int UnestimatedWorkItems { get; init; }

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

    /// <summary>
    /// When the sprint became Active: when the team started it, or else the start of its first planned day in
    /// <see cref="TimeZone"/>. Null for a sprint whose team is not mapped.
    /// </summary>
    public Instant? ActiveFrom { get; init; }

    /// <summary>
    /// When the sprint stops being Active, exclusive: a sprint that runs to the end of a day ends at the next
    /// midnight, so its last day is the one before.
    /// </summary>
    public Instant? ActiveUntil { get; init; }

    /// <summary>The IANA time zone the sprint's days are counted in: its team's.</summary>
    public string? TimeZone { get; init; }
    public required NavigationDto Team { get; init; }

    /// <summary>
    /// The estimate the sprint is measured in: its team's sizing method on the sprint's planned start. Under
    /// Count every estimate equals its item count.
    /// </summary>
    public SizingMethod SizingMethod { get; init; }

    // Metrics
    public int TotalWorkItems { get; init; }
    public double TotalEstimate { get; init; }
    public int CompletedWorkItems { get; init; }
    public double CompletedEstimate { get; init; }
    public int InProgressWorkItems { get; init; }
    public double InProgressEstimate { get; init; }
    public int NotStartedWorkItems { get; init; }
    public double NotStartedEstimate { get; init; }

    /// <summary>Items with no value in <see cref="SizingMethod"/>. An estimate of 0 is an estimate.</summary>
    public int UnestimatedWorkItems { get; init; }

    /// <summary>Cycle-time rollup for this sprint.</summary>
    public required CycleTimeSummary CycleTime { get; init; }

    /// <summary>
    /// The sizing method shared by every one of <paramref name="sprints"/>, or null when they use different
    /// ones or there are none.
    /// </summary>
    public static SizingMethod? CommonSizingMethod(IReadOnlyCollection<SprintMetricsSummary> sprints)
    {
        var methods = sprints.Select(s => s.SizingMethod).Distinct().ToList();
        return methods.Count == 1 ? methods[0] : null;
    }
}
