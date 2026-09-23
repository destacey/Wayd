namespace Wayd.Work.Domain.Models.BacklogHealth;

/// <summary>
/// The limits a backlog health assessment grades against. Every value has a default, so a
/// caller overrides only the ones it wants to explore.
/// </summary>
/// <remarks>
/// Item checks share one grading scale: the percent of the items in scope that were flagged.
/// Rank inversions have none, because any inversion is a defect the team can fix in place.
/// </remarks>
public sealed record BacklogHealthThresholds
{
    public static BacklogHealthThresholds Default { get; } = new();

    public int StaleDays { get; init; } = 90;

    public int OldProposedDays { get; init; } = 180;

    public int AgingWipPercentile { get; init; } = 85;

    public int OversizedPercentile { get; init; } = 85;

    public int ReadinessWindowWeeks { get; init; } = 4;

    /// <summary>
    /// The readiness window's size, in top-ranked items, when there is too little history to
    /// size it from throughput.
    /// </summary>
    public int ReadinessFallbackItems { get; init; } = 20;

    public int AtRiskPercent { get; init; } = 10;

    public int UnhealthyPercent { get; init; } = 25;

    public double RunwayAtRiskWeeks { get; init; } = 4;

    public double RunwayUnhealthyWeeks { get; init; } = 2;

    /// <summary>
    /// A runway longer than this is At Risk: refining that much work ahead of time is waste.
    /// </summary>
    public double RunwayTooLongWeeks { get; init; } = 26;

    public double NetFlowAtRisk { get; init; } = 1.2;

    public double NetFlowUnhealthy { get; init; } = 1.5;

    public double WipLoadAtRisk { get; init; } = 1.5;

    public double WipLoadUnhealthy { get; init; } = 2.0;
}
