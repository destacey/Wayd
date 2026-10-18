using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Web.Api.Models.Planning.PlanningIntervals;

/// <summary>
/// Response containing PI Iteration metrics aggregated across the sprints mapped to it and the planning
/// interval's Kanban teams, which are measured by the iteration's dates.
/// </summary>
public sealed record PlanningIntervalIterationMetricsResponse
{
    public Guid IterationId { get; init; }
    public int IterationKey { get; init; }
    public required string IterationName { get; init; }
    public required LocalDate Start { get; init; }
    public required LocalDate End { get; init; }
    public required SimpleNavigationDto Category { get; init; }

    /// <summary>Teams measured in the iteration: those with a sprint mapped to it, and its Kanban teams.</summary>
    public int TeamCount { get; init; }

    public int SprintCount { get; init; }

    /// <summary>
    /// The sizing method every sprint and Kanban team in the iteration is measured in, which
    /// <c>completedEstimate</c> and <c>inProgressEstimate</c> are in. Null when they use different sizing methods,
    /// or there are none: estimates in different units are never added, so those totals are null too and only the
    /// counts roll up.
    /// </summary>
    public SizingMethod? SizingMethod { get; init; }

    /// <summary>
    /// The sizing method every sprint in the iteration is measured in, which <c>totalEstimate</c> and
    /// <c>notStartedEstimate</c> are in, since only sprints add to them. Null when the sprints use different
    /// sizing methods, or there are none. Kanban teams play no part, so this is the unit of the iteration's
    /// backlog.
    /// </summary>
    public SizingMethod? SprintSizingMethod { get; init; }

    /// <summary>
    /// Items scoped to the iteration's sprints. Kanban teams have no scope, so they add nothing here; take a
    /// completion rate from the sprint entries, not from this and <c>completedWorkItems</c>.
    /// </summary>
    public int TotalWorkItems { get; init; }
    public double? TotalEstimate { get; init; }

    /// <summary>Items completed in the iteration's sprints, plus those the Kanban teams completed within its dates.</summary>
    public int CompletedWorkItems { get; init; }
    public double? CompletedEstimate { get; init; }

    /// <summary>Items in progress in the iteration's sprints, plus those in progress for the Kanban teams.</summary>
    public int InProgressWorkItems { get; init; }
    public double? InProgressEstimate { get; init; }

    /// <summary>Items not started in the iteration's sprints. Kanban teams have no scope, so they add nothing here.</summary>
    public int NotStartedWorkItems { get; init; }
    public double? NotStartedEstimate { get; init; }

    /// <summary>Items with no value in their own sprint's or Kanban team's sizing method, across all of them.</summary>
    public int UnestimatedWorkItems { get; init; }

    /// <summary>Cycle-time rollup across all sprints and Kanban teams in this iteration.</summary>
    public required CycleTimeSummary CycleTime { get; init; }

    /// <summary>One entry per sprint mapped to the iteration.</summary>
    public required IReadOnlyList<SprintMetricsSummary> SprintMetrics { get; init; }

    /// <summary>
    /// One entry per Kanban team in the planning interval with no sprint mapped to the iteration, measured by the
    /// iteration's dates rather than by a sprint.
    /// </summary>
    public required IReadOnlyList<DateMeasuredTeamMetricsSummary> DateMeasuredTeamMetrics { get; init; }

    /// <summary>
    /// The planning interval teams among <paramref name="piTeamIds"/> with no sprint mapped to
    /// <paramref name="iteration"/>, which are the ones that can be measured by its dates: a team with a mapped
    /// sprint is measured by that sprint.
    /// </summary>
    public static List<Guid> TeamsWithoutSprint(IEnumerable<Guid> piTeamIds, PlanningIntervalIterationSprintsDto iteration)
    {
        var sprintTeamIds = iteration.Sprints.Select(s => s.Team.Id).ToHashSet();
        return [.. piTeamIds.Where(id => !sprintTeamIds.Contains(id)).Distinct()];
    }

    /// <summary>
    /// Rolls <paramref name="sprints"/> and <paramref name="teams"/> up into the iteration's metrics. Kanban teams
    /// add to the completed, in-progress, unestimated and cycle-time totals but not to the total or not-started
    /// ones, which are scoped to sprints.
    /// </summary>
    public static PlanningIntervalIterationMetricsResponse Create(
        PlanningIntervalIterationSprintsDto iteration,
        IReadOnlyList<SprintMetricsSummary> sprints,
        IReadOnlyList<DateMeasuredTeamMetricsSummary> teams)
    {
        var sizingMethod = SprintMetricsSummary.CommonSizingMethod(sprints, teams);
        var sprintSizingMethod = SprintMetricsSummary.CommonSizingMethod(sprints);

        double? SprintTotal(Func<SprintMetricsSummary, double> estimate) =>
            sprintSizingMethod is null ? null : sprints.Sum(estimate);
        double? CombinedTotal(Func<SprintMetricsSummary, double> sprintEstimate, Func<DateMeasuredTeamMetricsSummary, double> teamEstimate) =>
            sizingMethod is null ? null : sprints.Sum(sprintEstimate) + teams.Sum(teamEstimate);

        return new PlanningIntervalIterationMetricsResponse
        {
            IterationId = iteration.Id,
            IterationKey = iteration.Key,
            IterationName = iteration.Name,
            Start = iteration.Start,
            End = iteration.End,
            Category = iteration.Category,
            TeamCount = sprints.Select(s => s.Team.Id).Concat(teams.Select(t => t.Team.Id)).Distinct().Count(),
            SprintCount = sprints.Count,
            SizingMethod = sizingMethod,
            SprintSizingMethod = sprintSizingMethod,
            TotalWorkItems = sprints.Sum(s => s.TotalWorkItems),
            TotalEstimate = SprintTotal(s => s.TotalEstimate),
            CompletedWorkItems = sprints.Sum(s => s.CompletedWorkItems) + teams.Sum(t => t.CompletedWorkItems),
            CompletedEstimate = CombinedTotal(s => s.CompletedEstimate, t => t.CompletedEstimate),
            InProgressWorkItems = sprints.Sum(s => s.InProgressWorkItems) + teams.Sum(t => t.InProgressWorkItems),
            InProgressEstimate = CombinedTotal(s => s.InProgressEstimate, t => t.InProgressEstimate),
            NotStartedWorkItems = sprints.Sum(s => s.NotStartedWorkItems),
            NotStartedEstimate = SprintTotal(s => s.NotStartedEstimate),
            UnestimatedWorkItems = sprints.Sum(s => s.UnestimatedWorkItems) + teams.Sum(t => t.UnestimatedWorkItems),
            CycleTime = CycleTimeSummary.Combine(sprints.Select(s => s.CycleTime).Concat(teams.Select(t => t.CycleTime))),
            SprintMetrics = sprints,
            DateMeasuredTeamMetrics = teams,
        };
    }
}

/// <summary>
/// A Kanban team's metrics for the PI Iteration, measured by the iteration's dates in the team's time zone. Nothing
/// is scoped to an iteration for a Kanban team, so it has no total, committed or not-started figures.
/// </summary>
public sealed record DateMeasuredTeamMetricsSummary
{
    public required NavigationDto Team { get; init; }

    /// <summary>
    /// When the measured window begins: the start of the iteration's first day in the team's time zone that day,
    /// or in its zone on the last day when it has no operating model on the first.
    /// </summary>
    public Instant From { get; init; }

    /// <summary>
    /// When the measured window ends, exclusive: the start of the day after the iteration's last, in
    /// <c>timeZone</c>.
    /// </summary>
    public Instant Until { get; init; }

    /// <summary>The IANA time zone the team's operating model has on the iteration's last day.</summary>
    public required string TimeZone { get; init; }

    /// <summary>
    /// The estimate the team is measured in: its sizing method on the iteration's first day, or on its last when
    /// its operating models begin inside the iteration.
    /// </summary>
    public SizingMethod SizingMethod { get; init; }

    /// <summary>Requirement-tier items that reached a Done-category status within the window, by done date. Removed work is left out.</summary>
    public int CompletedWorkItems { get; init; }
    public double CompletedEstimate { get; init; }

    /// <summary>Requirement-tier items in an In Progress-category status at <c>inProgressAsOf</c>.</summary>
    public int InProgressWorkItems { get; init; }
    public double InProgressEstimate { get; init; }

    /// <summary>
    /// When in progress was read: the window's end for a past iteration, now for the current one, and null for a
    /// future one.
    /// </summary>
    public Instant? InProgressAsOf { get; init; }

    /// <summary>Completed and in-progress items with no value in <c>sizingMethod</c>.</summary>
    public int UnestimatedWorkItems { get; init; }

    /// <summary>Cycle-time rollup of the completed items.</summary>
    public required CycleTimeSummary CycleTime { get; init; }

    /// <summary>
    /// Whether in progress for a past iteration may be short, because a workspace holding the team's work has not
    /// had its history read through to the end.
    /// </summary>
    public bool IsHistoryIncomplete { get; init; }
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
    /// <c>TimeZone</c>. Null for a sprint whose team is not mapped.
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

    /// <summary>Items with no value in <c>SizingMethod</c>. An estimate of 0 is an estimate.</summary>
    public int UnestimatedWorkItems { get; init; }

    /// <summary>Cycle-time rollup for this sprint.</summary>
    public required CycleTimeSummary CycleTime { get; init; }

    /// <summary>
    /// The sizing method shared by every one of <paramref name="sprints"/> and <paramref name="teams"/>, or null
    /// when they use different ones or there are none.
    /// </summary>
    public static SizingMethod? CommonSizingMethod(IReadOnlyCollection<SprintMetricsSummary> sprints, IReadOnlyCollection<DateMeasuredTeamMetricsSummary>? teams = null)
    {
        var methods = sprints.Select(s => s.SizingMethod)
            .Concat(teams?.Select(t => t.SizingMethod) ?? [])
            .Distinct()
            .ToList();
        return methods.Count == 1 ? methods[0] : null;
    }
}
