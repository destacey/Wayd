using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Work.Domain.Models.SprintMetrics;

namespace Wayd.Work.Domain.Models.CadenceMetrics;

/// <summary>A work item a team completed, with the estimates it carries now.</summary>
/// <param name="DoneAt">When the item reached a Done-category status.</param>
public sealed record CompletedWorkItem(Instant DoneAt, double? StoryPoints, double? Effort, double? Size);

/// <summary>What a team completed in one of its reporting periods.</summary>
/// <param name="CompletedCount">How many items were completed in the period.</param>
/// <param name="CompletedEstimate">The completed items' estimates in the period's sizing method; under Count, the count.</param>
/// <param name="UnestimatedCount">Completed items with no value in the period's sizing method, which add nothing to <paramref name="CompletedEstimate"/>.</param>
/// <param name="InAverages">Whether the period counts toward the averages: it has ended and is not partial.</param>
/// <param name="InEstimateAverages">
/// Whether the period counts toward the estimate averages: it counts toward the averages and was measured in the
/// report's sizing method.
/// </param>
public sealed record MeasuredCadencePeriod(
    CadencePeriod Period,
    int CompletedCount,
    double CompletedEstimate,
    int UnestimatedCount,
    bool InAverages,
    bool InEstimateAverages);

/// <summary>
/// A Kanban team's velocity over its reporting cadence: what it completed in each recent period, from done dates,
/// and the average and spread across them. Worked out when read and never stored.
/// </summary>
/// <remarks>
/// A cadence is a reporting boundary only, so there is no commitment and no predictability or say/do. Velocity is
/// the Requirement-tier work that reached a Done-category status in the period; Removed work is not velocity
/// here, as there was no commitment for it to leave.
/// </remarks>
public sealed class TeamCadenceMetrics
{
    private TeamCadenceMetrics(SizingMethod sizingMethod, IReadOnlyList<MeasuredCadencePeriod> periods)
    {
        SizingMethod = sizingMethod;
        Periods = periods;
    }

    /// <summary>The estimate the report is measured in: the team's sizing method now.</summary>
    public SizingMethod SizingMethod { get; }

    /// <summary>The current period, if any, then the ended periods, most recent first.</summary>
    public IReadOnlyList<MeasuredCadencePeriod> Periods { get; }

    /// <summary>Velocity by item count across the periods in the averages; null when there are none.</summary>
    public SprintMetricSummary? ByCount =>
        SprintMetricSummary.Of(Periods.Where(p => p.InAverages).Select(p => (double?)p.CompletedCount));

    /// <summary>
    /// Velocity in <see cref="SizingMethod"/> across the averaged periods measured in it: estimates in different
    /// units are never averaged. A team that sizes by count is measured by count, across every averaged period.
    /// </summary>
    public SprintMetricSummary? ByEstimate => SizingMethod == SizingMethod.Count
        ? ByCount
        : SprintMetricSummary.Of(Periods.Where(p => p.InEstimateAverages).Select(p => (double?)p.CompletedEstimate));

    /// <summary>Measures each of <paramref name="periods"/> from the items completed in it.</summary>
    /// <param name="sizingMethod">The team's sizing method now, which the estimate averages are in.</param>
    public static TeamCadenceMetrics Measure(SizingMethod sizingMethod, IReadOnlyList<CadencePeriod> periods, IReadOnlyCollection<CompletedWorkItem> completions)
    {
        var measured = periods
            .Select(period =>
            {
                var items = completions.Where(c => c.DoneAt >= period.From && c.DoneAt < period.Until).ToList();
                var estimates = items.Select(i => WorkItemEstimate.Of(period.SizingMethod, i.StoryPoints, i.Effort, i.Size)).ToList();
                var inAverages = !period.IsCurrent && !period.IsPartial;

                return new MeasuredCadencePeriod(
                    period,
                    items.Count,
                    estimates.Sum(e => e ?? 0),
                    estimates.Count(e => e is null),
                    inAverages,
                    inAverages && period.SizingMethod == sizingMethod);
            })
            .ToList();

        return new TeamCadenceMetrics(sizingMethod, measured);
    }
}
