using Wayd.Common.Domain.Enums.Organization;
using Wayd.Work.Domain.Models.CadenceMetrics;
using Wayd.Work.Domain.Models.SprintMetrics;

namespace Wayd.Work.Application.WorkTeams.Dtos;

/// <summary>
/// A Kanban team's velocity over its reporting cadence: what it completed in each recent period, from done dates,
/// and the average and spread across them. Worked out when read, and never stored. There is no commitment, so no
/// predictability or say/do.
/// </summary>
public sealed record TeamCadenceMetricsDto
{
    /// <summary>The team the metrics are of.</summary>
    public Guid TeamId { get; init; }

    /// <summary>How many ended periods the window holds when full: the number asked for, or else <see cref="DefaultPeriodWindow"/>.</summary>
    public int PeriodWindow { get; init; }

    /// <summary>The organization's window, from the sprint metrics setting, which team pages are measured over.</summary>
    public int DefaultPeriodWindow { get; init; }

    /// <summary>
    /// The length in weeks of the team's cadence now, or null when it has none. A team can have periods from an
    /// earlier cadence while having none now.
    /// </summary>
    public int? CadenceWeeks { get; init; }

    /// <summary>
    /// The estimate <see cref="ByEstimate"/> is measured in: the team's sizing method now. Under Count
    /// <see cref="ByEstimate"/> equals <see cref="ByCount"/>.
    /// </summary>
    public SizingMethod SizingMethod { get; init; }

    /// <summary>Velocity by item count across the ended, whole periods; null when there are none.</summary>
    public CadenceVelocitySummaryDto? ByCount { get; init; }

    /// <summary>
    /// Velocity in <see cref="SizingMethod"/> across the ended, whole periods measured in it; null when there are
    /// none. Estimates in different units are never averaged.
    /// </summary>
    public CadenceVelocitySummaryDto? ByEstimate { get; init; }

    /// <summary>
    /// The current period, if any, then up to <see cref="PeriodWindow"/> ended periods, most recent first. Empty
    /// for a team that has never had a cadence.
    /// </summary>
    public required List<TeamCadencePeriodDto> Periods { get; init; }

    /// <summary>The DTO of <paramref name="metrics"/>, measured over <paramref name="periodWindow"/> ended periods.</summary>
    public static TeamCadenceMetricsDto From(Guid teamId, int periodWindow, int defaultPeriodWindow, int? cadenceWeeks, TeamCadenceMetrics metrics) => new()
    {
        TeamId = teamId,
        PeriodWindow = periodWindow,
        DefaultPeriodWindow = defaultPeriodWindow,
        CadenceWeeks = cadenceWeeks,
        SizingMethod = metrics.SizingMethod,
        ByCount = CadenceVelocitySummaryDto.From(metrics.ByCount),
        ByEstimate = CadenceVelocitySummaryDto.From(metrics.ByEstimate),
        Periods = [.. metrics.Periods.Select(TeamCadencePeriodDto.From)],
    };
}

/// <summary>What a team completed in one of its reporting periods.</summary>
public sealed record TeamCadencePeriodDto
{
    /// <summary>The period's first day.</summary>
    public LocalDate Start { get; init; }

    /// <summary>The period's last day, included in it.</summary>
    public LocalDate End { get; init; }

    /// <summary>The estimate the period's work is measured in: its operating model's sizing method.</summary>
    public SizingMethod SizingMethod { get; init; }

    /// <summary>
    /// Whether the period is shorter than its cadence, because an operating model began or ended inside it.
    /// Partial periods are left out of the averages.
    /// </summary>
    public bool IsPartial { get; init; }

    /// <summary>Whether the period has not ended yet. The current period is left out of the averages.</summary>
    public bool IsCurrent { get; init; }

    /// <summary>Whether the period counts toward the averages: it has ended and is whole.</summary>
    public bool InAverages { get; init; }

    /// <summary>Whether <see cref="CompletedEstimate"/> counts toward the estimate averages.</summary>
    public bool InEstimateAverages { get; init; }

    /// <summary>How many Requirement-tier items reached a Done-category status in the period.</summary>
    public int CompletedCount { get; init; }

    /// <summary>The completed items' estimates in <see cref="SizingMethod"/>; under Count, the count.</summary>
    public double CompletedEstimate { get; init; }

    /// <summary>Completed items with no value in <see cref="SizingMethod"/>, which add nothing to <see cref="CompletedEstimate"/>.</summary>
    public int UnestimatedCount { get; init; }

    /// <summary>The DTO of one measured period.</summary>
    public static TeamCadencePeriodDto From(MeasuredCadencePeriod measured) => new()
    {
        Start = measured.Period.Start,
        End = measured.Period.End,
        SizingMethod = measured.Period.SizingMethod,
        IsPartial = measured.Period.IsPartial,
        IsCurrent = measured.Period.IsCurrent,
        InAverages = measured.InAverages,
        InEstimateAverages = measured.InEstimateAverages,
        CompletedCount = measured.CompletedCount,
        CompletedEstimate = measured.CompletedEstimate,
        UnestimatedCount = measured.UnestimatedCount,
    };
}

/// <summary>Velocity across a team's averaged periods.</summary>
/// <param name="Average">The mean of the periods' values, not a ratio of pooled totals.</param>
/// <param name="Min">The lowest period's value.</param>
/// <param name="Max">The highest period's value.</param>
/// <param name="StandardDeviation">The population standard deviation of the periods' values.</param>
/// <param name="Periods">How many periods were averaged.</param>
public sealed record CadenceVelocitySummaryDto(double Average, double Min, double Max, double StandardDeviation, int Periods)
{
    /// <summary>The DTO of a velocity summary, or null when no period was averaged.</summary>
    public static CadenceVelocitySummaryDto? From(SprintMetricSummary? summary) =>
        summary is null ? null : new(summary.Average, summary.Min, summary.Max, summary.StandardDeviation, summary.Sprints);
}
