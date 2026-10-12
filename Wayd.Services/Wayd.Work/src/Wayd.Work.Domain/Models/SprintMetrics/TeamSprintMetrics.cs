using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Domain.Models.SprintScope;

namespace Wayd.Work.Domain.Models.SprintMetrics;

/// <summary>A sprint's velocity, predictability and say/do in one unit, read from its scope.</summary>
/// <param name="Velocity">What was completed while in the sprint, including work completed as Removed.</param>
/// <param name="Predictability">Velocity ÷ committed, up to 1; null when there is no commitment in this unit.</param>
/// <param name="SayDo">Completed of committed ÷ committed; null when there is no commitment in this unit.</param>
public sealed record SprintMetricFigures(double Velocity, double? Predictability, double? SayDo)
{
    /// <summary>The figures by item count.</summary>
    public static SprintMetricFigures ByCount(SprintScopeTotals totals) =>
        new(totals.Completed.Count, totals.PredictabilityCount, totals.SayDoCount);

    /// <summary>The figures in the estimate the sprint was measured in.</summary>
    public static SprintMetricFigures ByEstimate(SprintScopeTotals totals) =>
        new(totals.Completed.Estimate, totals.PredictabilityEstimate, totals.SayDoEstimate);
}

/// <summary>A sprint counted in its team's sprint metrics.</summary>
/// <param name="SizingMethod">The estimate the sprint was measured in: its team's on the sprint's planned start.</param>
/// <param name="Totals">The sprint's scope, which its figures are read from.</param>
public sealed record MeasuredSprint(Iteration Sprint, SizingMethod SizingMethod, SprintScopeTotals Totals)
{
    /// <summary>The sprint's figures by item count.</summary>
    public SprintMetricFigures ByCount => SprintMetricFigures.ByCount(Totals);

    /// <summary>The sprint's figures in <see cref="SizingMethod"/>.</summary>
    public SprintMetricFigures ByEstimate => SprintMetricFigures.ByEstimate(Totals);
}

/// <summary>A completed sprint left out of its team's sprint metrics, and why.</summary>
public sealed record ExcludedSprint(Iteration Sprint, SprintMetricsExclusion Reason);

/// <summary>Each metric summarized across the sprints that have a value for it, in one unit.</summary>
public sealed record SprintMetricSummaries(SprintMetricSummary? Velocity, SprintMetricSummary? Predictability, SprintMetricSummary? SayDo)
{
    /// <summary>Summarizes each metric across <paramref name="figures"/>.</summary>
    public static SprintMetricSummaries Of(IReadOnlyCollection<SprintMetricFigures> figures) =>
        new(
            SprintMetricSummary.Of(figures.Select(f => (double?)f.Velocity)),
            SprintMetricSummary.Of(figures.Select(f => f.Predictability)),
            SprintMetricSummary.Of(figures.Select(f => f.SayDo)));
}

/// <summary>
/// A team's velocity, predictability and say/do over its last completed standard sprints. Worked out when read
/// and never stored, from the same scope each sprint reports on its own.
/// </summary>
/// <remarks>
/// The team's completed sprints are assessed from the most recent back until the window is full. A sprint is
/// left out, and listed with its reason, when it is non-standard, its work item history is incomplete, or
/// nothing was committed to it. Only the sprints assessed on the way to filling the window are listed, and no
/// more than <see cref="LookbackFactor"/> times the window are assessed, so a team with a long run of unusable
/// sprints gets a short report rather than a slow one.
/// </remarks>
public sealed class TeamSprintMetrics
{
    /// <summary>How many times the window's size of completed sprints are assessed, at most, to fill it.</summary>
    public const int LookbackFactor = 3;

    private readonly List<MeasuredSprint> _sprints = [];
    private readonly List<ExcludedSprint> _excluded = [];

    /// <param name="size">
    /// How many sprints the window holds, held to <see cref="SprintMetricsSettings.MinSprintWindow"/>–<see cref="SprintMetricsSettings.MaxSprintWindow"/>.
    /// </param>
    public TeamSprintMetrics(int size)
    {
        Size = Math.Clamp(size, SprintMetricsSettings.MinSprintWindow, SprintMetricsSettings.MaxSprintWindow);
    }

    /// <summary>How many sprints the window holds when full.</summary>
    public int Size { get; }

    /// <summary>Whether the window holds <see cref="Size"/> sprints, so no earlier sprint needs assessing.</summary>
    public bool IsFull => _sprints.Count >= Size;

    /// <summary>The sprints counted, most recent first. Fewer than <see cref="Size"/> when the team has too few.</summary>
    public IReadOnlyList<MeasuredSprint> Sprints => _sprints;

    /// <summary>The sprints passed over while filling the window, most recent first.</summary>
    public IReadOnlyList<ExcludedSprint> Excluded => _excluded;

    /// <summary>
    /// The estimate the report is measured in: the most recent counted sprint's, which is the unit the team
    /// plans its next sprint in. Count when no sprint is counted.
    /// </summary>
    public SizingMethod SizingMethod => _sprints.Count == 0 ? SizingMethod.Count : _sprints[0].SizingMethod;

    /// <summary>The metrics by item count, across every counted sprint.</summary>
    public SprintMetricSummaries ByCount => SprintMetricSummaries.Of([.. _sprints.Select(s => s.ByCount)]);

    /// <summary>
    /// The metrics in <see cref="SizingMethod"/>, across the counted sprints measured in it. A sprint measured
    /// in another estimate, because the team changed its sizing method within the window, is left out:
    /// estimates in different units are never averaged.
    /// </summary>
    public SprintMetricSummaries ByEstimate => SprintMetricSummaries.Of([.. _sprints.Where(IsInReportEstimate).Select(s => s.ByEstimate)]);

    /// <summary>Whether <paramref name="sprint"/> was measured in the report's estimate, so it counts toward <see cref="ByEstimate"/>.</summary>
    public bool IsInReportEstimate(MeasuredSprint sprint) => sprint.SizingMethod == SizingMethod;

    /// <summary>
    /// The sprints to assess, in the order to assess them: the team's sprints that have ended by
    /// <paramref name="now"/>, most recent first.
    /// </summary>
    public IReadOnlyList<Iteration> Candidates(TeamSprintTimeline timeline, Instant now) =>
        [.. timeline.Sprints
            .Reverse()
            .Where(s => timeline.StateAt(s, now) == IterationState.Completed)
            .Take(Size * LookbackFactor)];

    /// <summary>
    /// Whether <paramref name="type"/> keeps a sprint in the running. A non-standard sprint is left out here,
    /// before its scope is worked out, and the caller moves on to the next candidate.
    /// </summary>
    public bool Admit(Iteration sprint, ResolvedSprintType type)
    {
        if (type.Type != SprintType.NonStandard)
            return true;

        _excluded.Add(new ExcludedSprint(sprint, SprintMetricsExclusion.NonStandard));
        return false;
    }

    /// <summary>
    /// Counts an admitted sprint, or leaves it out when its history is incomplete or nothing was committed to it.
    /// Sprints are assessed most recent first.
    /// </summary>
    public void Assess(Iteration sprint, SizingMethod sizingMethod, bool historyIncomplete, SprintScopeTotals totals)
    {
        if (IsFull)
            throw new InvalidOperationException("The window is full.");

        if (historyIncomplete)
            _excluded.Add(new ExcludedSprint(sprint, SprintMetricsExclusion.HistoryIncomplete));
        else if (totals.Committed.Count == 0)
            _excluded.Add(new ExcludedSprint(sprint, SprintMetricsExclusion.NoCommitment));
        else
            _sprints.Add(new MeasuredSprint(sprint, sizingMethod, totals));
    }
}
