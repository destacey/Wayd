using Ardalis.GuardClauses;
using NodaTime;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Enums.Work;

namespace Wayd.Work.Domain.Models.BacklogHealth;

/// <summary>
/// Grades a team's backlog against a set of <see cref="BacklogHealthThresholds"/>.
/// </summary>
public static class BacklogHealthAssessor
{
    /// <summary>
    /// Fewer completions than this is too little history for the checks that measure the team
    /// against its own past. Matches the throughput forecast's minimum.
    /// </summary>
    public const int MinimumItemsCompleted = 10;

    /// <param name="memberCount">The team's members, or null when unknown.</param>
    /// <param name="usesProjects">
    /// The team links its work to projects. Without that, a missing project is not a gap.
    /// </param>
    public static BacklogHealthAssessment Assess(
        IReadOnlyCollection<BacklogHealthItem> backlog,
        BacklogHealthHistory history,
        int? memberCount,
        bool usesProjects,
        Instant now,
        BacklogHealthThresholds thresholds)
    {
        Guard.Against.Null(backlog);
        Guard.Against.Null(history);
        Guard.Against.Null(thresholds);

        var ranked = backlog.OrderBy(i => i.Rank).ToList();
        var flags = ranked.ToDictionary(i => i.Id, _ => new List<BacklogHealthCheck>());
        var results = new List<BacklogHealthCheckResult>();

        var completed = history.Completions.Count;
        var hasHistory = completed >= MinimumItemsCompleted;

        // Divide last, so a runway that lands exactly on a threshold is graded as that value.
        results.Add(hasHistory
            ? Runway((double)ranked.Count * history.LookbackDays / (7.0 * completed), thresholds)
            : NotAssessed(BacklogHealthCheck.Runway, BacklogHealthOutcome.NotEnoughHistory));
        results.Add(NetFlow(history, thresholds));
        results.Add(WipLoad(ranked.Count(i => i.StatusCategory == WorkStatusCategory.Active), memberCount, thresholds));

        void Flag(BacklogHealthCheck check, IEnumerable<BacklogHealthItem> inScope, Func<BacklogHealthItem, bool> isFlagged, bool anyIsUnhealthy = false)
        {
            var scope = inScope.ToList();
            if (scope.Count == 0)
            {
                results.Add(NotAssessed(check, BacklogHealthOutcome.NotApplicable));
                return;
            }

            var flagged = scope.Where(isFlagged).ToList();
            foreach (var item in flagged)
                flags[item.Id].Add(check);

            var percent = 100.0 * flagged.Count / scope.Count;
            var grade = anyIsUnhealthy
                ? flagged.Count > 0 ? HealthStatus.Unhealthy : HealthStatus.Healthy
                : percent >= thresholds.UnhealthyPercent ? HealthStatus.Unhealthy
                : percent >= thresholds.AtRiskPercent ? HealthStatus.AtRisk
                : HealthStatus.Healthy;

            results.Add(new BacklogHealthCheckResult(check, BacklogHealthOutcome.Assessed, grade, percent, flagged.Count, scope.Count));
        }

        var active = ranked.Where(i => i.StatusCategory == WorkStatusCategory.Active).ToList();

        Flag(BacklogHealthCheck.Stale, ranked,
            i => now - i.LastModified >= Duration.FromDays(thresholds.StaleDays));

        Flag(BacklogHealthCheck.OldProposed, ranked.Where(i => i.StatusCategory == WorkStatusCategory.Proposed),
            i => now - i.Created >= Duration.FromDays(thresholds.OldProposedDays));

        var cycleTimes = history.Completions
            .Where(c => c.Activated.HasValue && c.Done > c.Activated.Value)
            .Select(c => (c.Done - c.Activated!.Value).TotalDays)
            .ToList();
        double? agingWipDays = cycleTimes.Count >= MinimumItemsCompleted
            ? Percentile(cycleTimes, thresholds.AgingWipPercentile)
            : null;

        // An active item with no activation time cannot be aged, so it is out of scope.
        if (agingWipDays is { } agingLimit)
            Flag(BacklogHealthCheck.AgingWip, active.Where(i => i.Activated.HasValue),
                i => (now - i.Activated!.Value).TotalDays > agingLimit);
        else
            results.Add(NotAssessed(BacklogHealthCheck.AgingWip, BacklogHealthOutcome.NotEnoughHistory));

        var windowSize = hasHistory
            ? Math.Max(1, (int)Math.Ceiling((double)completed * thresholds.ReadinessWindowWeeks * 7 / history.LookbackDays))
            : thresholds.ReadinessFallbackItems;
        var window = ranked.Take(windowSize).ToList();

        Flag(BacklogHealthCheck.MissingStoryPoints, window, i => i.StoryPoints is null);

        var estimates = history.Completions.Where(c => c.StoryPoints.HasValue).Select(c => c.StoryPoints!.Value).ToList();
        double? oversizedStoryPoints = estimates.Count >= MinimumItemsCompleted
            ? Percentile(estimates, thresholds.OversizedPercentile)
            : null;

        if (oversizedStoryPoints is { } oversizedLimit)
            Flag(BacklogHealthCheck.Oversized, window.Where(i => i.StoryPoints.HasValue),
                i => i.StoryPoints!.Value > oversizedLimit);
        else
            results.Add(NotAssessed(BacklogHealthCheck.Oversized, BacklogHealthOutcome.NotEnoughHistory));

        Flag(BacklogHealthCheck.NoParent, window, i => !i.HasParent);

        if (usesProjects)
            Flag(BacklogHealthCheck.NoProject, window, i => !i.HasProject);
        else
            results.Add(NotAssessed(BacklogHealthCheck.NoProject, BacklogHealthOutcome.NotApplicable));

        Flag(BacklogHealthCheck.UnassignedActive, active, i => !i.IsAssigned);

        Flag(BacklogHealthCheck.CarryOver, ranked.Where(i => i.SprintState.HasValue),
            i => i.SprintState == IterationState.Completed);

        Flag(BacklogHealthCheck.ClosedParent, ranked.Where(i => i.HasParent), i => i.IsParentClosed);

        var rankById = ranked.ToDictionary(i => i.Id, i => i.Rank);
        Flag(BacklogHealthCheck.RankInversion,
            ranked.Where(i => i.PredecessorIds.Any(rankById.ContainsKey)),
            i => i.PredecessorIds.Any(p => rankById.TryGetValue(p, out var rank) && rank > i.Rank),
            anyIsUnhealthy: true);

        return new BacklogHealthAssessment(
            thresholds,
            [.. results.OrderBy(r => r.Check)],
            flags.ToDictionary(f => f.Key, f => (IReadOnlyList<BacklogHealthCheck>)f.Value),
            window.Count,
            agingWipDays,
            oversizedStoryPoints);
    }

    private static BacklogHealthCheckResult Runway(double weeks, BacklogHealthThresholds thresholds)
    {
        var grade = weeks < thresholds.RunwayUnhealthyWeeks ? HealthStatus.Unhealthy
            : weeks < thresholds.RunwayAtRiskWeeks || weeks > thresholds.RunwayTooLongWeeks ? HealthStatus.AtRisk
            : HealthStatus.Healthy;

        return new(BacklogHealthCheck.Runway, BacklogHealthOutcome.Assessed, grade, weeks, null, null);
    }

    private static BacklogHealthCheckResult NetFlow(BacklogHealthHistory history, BacklogHealthThresholds thresholds)
    {
        var completed = history.Completions.Count;
        if (completed == 0)
        {
            return history.ItemsCreated == 0
                ? NotAssessed(BacklogHealthCheck.NetFlow, BacklogHealthOutcome.NotApplicable)
                : new(BacklogHealthCheck.NetFlow, BacklogHealthOutcome.Assessed, HealthStatus.Unhealthy, null, null, null);
        }

        var ratio = (double)history.ItemsCreated / completed;
        return new(BacklogHealthCheck.NetFlow, BacklogHealthOutcome.Assessed, GradeAbove(ratio, thresholds.NetFlowAtRisk, thresholds.NetFlowUnhealthy), ratio, null, null);
    }

    private static BacklogHealthCheckResult WipLoad(int activeItems, int? memberCount, BacklogHealthThresholds thresholds)
    {
        if (memberCount is not > 0)
            return NotAssessed(BacklogHealthCheck.WipLoad, BacklogHealthOutcome.NotApplicable);

        var load = (double)activeItems / memberCount.Value;
        return new(BacklogHealthCheck.WipLoad, BacklogHealthOutcome.Assessed, GradeAbove(load, thresholds.WipLoadAtRisk, thresholds.WipLoadUnhealthy), load, null, null);
    }

    private static HealthStatus GradeAbove(double value, double atRisk, double unhealthy) =>
        value > unhealthy ? HealthStatus.Unhealthy
        : value > atRisk ? HealthStatus.AtRisk
        : HealthStatus.Healthy;

    private static BacklogHealthCheckResult NotAssessed(BacklogHealthCheck check, BacklogHealthOutcome outcome) =>
        new(check, outcome, null, null, null, null);

    /// <summary>
    /// The nearest-rank percentile: the smallest value at least <paramref name="percent"/>% of
    /// the values are at or below.
    /// </summary>
    internal static double Percentile(IReadOnlyCollection<double> values, int percent)
    {
        Guard.Against.NullOrEmpty(values);
        Guard.Against.OutOfRange(percent, nameof(percent), 1, 100);

        var sorted = values.Order().ToArray();
        return sorted[(int)Math.Ceiling(sorted.Length * percent / 100.0) - 1];
    }
}
