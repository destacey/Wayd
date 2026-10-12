using Wayd.Common.Domain.Enums.Organization;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Models.SprintMetrics;

namespace Wayd.Work.Application.Iterations.Dtos;

/// <summary>
/// A team's velocity, predictability and say/do over its last completed standard sprints: each sprint's
/// values, their average and their spread. Worked out when read from work item history, and never stored.
/// </summary>
public sealed record TeamSprintMetricsDto
{
    /// <summary>The team the metrics are of.</summary>
    public Guid TeamId { get; init; }

    /// <summary>How many sprints the window holds when full: the number asked for, or else <see cref="DefaultSprintWindow"/>.</summary>
    public int SprintWindow { get; init; }

    /// <summary>The organization's sprint window, from the system settings, which team pages are measured over.</summary>
    public int DefaultSprintWindow { get; init; }

    /// <summary>
    /// The estimate <see cref="ByEstimate"/> is measured in: the team's sizing method on its most recent counted
    /// sprint. Count when no sprint is counted. Under Count the estimate figures equal the count figures.
    /// </summary>
    public SizingMethod SizingMethod { get; init; }

    /// <summary>Each metric by item count, across every counted sprint.</summary>
    public required SprintMetricSummariesDto ByCount { get; init; }

    /// <summary>
    /// Each metric in <see cref="SizingMethod"/>, across the counted sprints measured in it. A sprint measured
    /// in another estimate is left out: estimates in different units are never averaged.
    /// </summary>
    public required SprintMetricSummariesDto ByEstimate { get; init; }

    /// <summary>
    /// The sprints counted, most recent first. Fewer than <see cref="SprintWindow"/> when the team has too few
    /// usable sprints among its most recent completed ones.
    /// </summary>
    public required List<TeamSprintMetricsSprintDto> Sprints { get; init; }

    /// <summary>
    /// The completed sprints passed over while filling the window, most recent first, each with why it was left
    /// out. Sprints older than the window reaches are not listed.
    /// </summary>
    public required List<TeamSprintMetricsExcludedSprintDto> Excluded { get; init; }

    public static TeamSprintMetricsDto From(Guid teamId, TeamSprintMetrics metrics, int defaultSprintWindow) => new()
    {
        TeamId = teamId,
        SprintWindow = metrics.Size,
        DefaultSprintWindow = defaultSprintWindow,
        SizingMethod = metrics.SizingMethod,
        ByCount = SprintMetricSummariesDto.From(metrics.ByCount),
        ByEstimate = SprintMetricSummariesDto.From(metrics.ByEstimate),
        Sprints = [.. metrics.Sprints.Select(s => TeamSprintMetricsSprintDto.From(s, metrics.IsInReportEstimate(s)))],
        Excluded = [.. metrics.Excluded.Select(TeamSprintMetricsExcludedSprintDto.From)],
    };
}

/// <summary>A sprint counted in its team's sprint metrics, with its own values.</summary>
public sealed record TeamSprintMetricsSprintDto
{
    public Guid Id { get; init; }

    /// <summary>The sprint's key, an alternate to its id.</summary>
    public int Key { get; init; }

    public required string Name { get; init; }

    /// <summary>The first planned day of the sprint.</summary>
    public LocalDate Start { get; init; }

    /// <summary>The last planned day of the sprint, included in it.</summary>
    public LocalDate End { get; init; }

    /// <summary>The estimate the sprint was measured in: its team's sizing method on the sprint's planned start.</summary>
    public SizingMethod SizingMethod { get; init; }

    /// <summary>
    /// Whether <see cref="ByEstimate"/> counts toward the report's estimate averages. False when the team has
    /// since changed its sizing method, so this sprint's estimates are in another unit.
    /// </summary>
    public bool InEstimateAverages { get; init; }

    /// <summary>The sprint's values by item count.</summary>
    public required SprintMetricFiguresDto ByCount { get; init; }

    /// <summary>The sprint's values in its own <see cref="SizingMethod"/>.</summary>
    public required SprintMetricFiguresDto ByEstimate { get; init; }

    public static TeamSprintMetricsSprintDto From(MeasuredSprint measured, bool inEstimateAverages) => new()
    {
        Id = measured.Sprint.Id,
        Key = measured.Sprint.Key,
        Name = measured.Sprint.Name,
        Start = measured.Sprint.DateRange.Start!.Value,
        End = measured.Sprint.DateRange.End!.Value,
        SizingMethod = measured.SizingMethod,
        InEstimateAverages = inEstimateAverages,
        ByCount = SprintMetricFiguresDto.From(measured.ByCount),
        ByEstimate = SprintMetricFiguresDto.From(measured.ByEstimate),
    };
}

/// <summary>A completed sprint left out of its team's sprint metrics.</summary>
public sealed record TeamSprintMetricsExcludedSprintDto
{
    public Guid Id { get; init; }

    /// <summary>The sprint's key, an alternate to its id.</summary>
    public int Key { get; init; }

    public required string Name { get; init; }

    /// <summary>The first planned day of the sprint.</summary>
    public LocalDate Start { get; init; }

    /// <summary>The last planned day of the sprint, included in it.</summary>
    public LocalDate End { get; init; }

    /// <summary>Why the sprint is left out: it is non-standard, its history is incomplete, or nothing was committed to it.</summary>
    public SprintMetricsExclusion Reason { get; init; }

    public static TeamSprintMetricsExcludedSprintDto From(ExcludedSprint excluded) => new()
    {
        Id = excluded.Sprint.Id,
        Key = excluded.Sprint.Key,
        Name = excluded.Sprint.Name,
        Start = excluded.Sprint.DateRange.Start!.Value,
        End = excluded.Sprint.DateRange.End!.Value,
        Reason = excluded.Reason,
    };
}

/// <summary>A sprint's velocity, predictability and say/do in one unit.</summary>
/// <param name="Velocity">What was completed while in the sprint, including work completed as Removed.</param>
/// <param name="Predictability">Velocity ÷ committed, up to 1; null when there is no commitment in this unit.</param>
/// <param name="SayDo">Completed of committed ÷ committed; null when there is no commitment in this unit.</param>
public sealed record SprintMetricFiguresDto(double Velocity, double? Predictability, double? SayDo)
{
    public static SprintMetricFiguresDto From(SprintMetricFigures figures) =>
        new(figures.Velocity, figures.Predictability, figures.SayDo);
}

/// <summary>Each metric summarized across the sprints that have a value for it; null when none has.</summary>
public sealed record SprintMetricSummariesDto(SprintMetricSummaryDto? Velocity, SprintMetricSummaryDto? Predictability, SprintMetricSummaryDto? SayDo)
{
    public static SprintMetricSummariesDto From(SprintMetricSummaries summaries) =>
        new(SprintMetricSummaryDto.From(summaries.Velocity), SprintMetricSummaryDto.From(summaries.Predictability), SprintMetricSummaryDto.From(summaries.SayDo));
}

/// <summary>One metric across a team's sprints.</summary>
/// <param name="Average">The mean of the sprints' values, not a ratio of their pooled totals.</param>
/// <param name="Min">The lowest sprint's value.</param>
/// <param name="Max">The highest sprint's value.</param>
/// <param name="StandardDeviation">The population standard deviation of the sprints' values.</param>
/// <param name="Sprints">How many sprints had a value for this metric.</param>
public sealed record SprintMetricSummaryDto(double Average, double Min, double Max, double StandardDeviation, int Sprints)
{
    public static SprintMetricSummaryDto? From(SprintMetricSummary? summary) =>
        summary is null ? null : new(summary.Average, summary.Min, summary.Max, summary.StandardDeviation, summary.Sprints);
}
