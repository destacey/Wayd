namespace Wayd.Work.Domain.Models.BacklogHealth;

/// <param name="Value">
/// For Runway, Net Flow and WIP Load, the measured weeks, ratio or load. For every other check,
/// the percent (0 to 100) of the items in scope that were flagged. Null when the check was not
/// assessed, and for a Net Flow with work created but none completed.
/// </param>
/// <param name="Flagged">The number of items flagged, for checks that flag items.</param>
/// <param name="InScope">The number of items the check looked at, for checks that flag items.</param>
public sealed record BacklogHealthCheckResult(
    BacklogHealthCheck Check,
    BacklogHealthOutcome Outcome,
    HealthStatus? Grade,
    double? Value,
    int? Flagged,
    int? InScope);

public sealed class BacklogHealthAssessment
{
    internal BacklogHealthAssessment(
        BacklogHealthThresholds thresholds,
        IReadOnlyList<BacklogHealthCheckResult> checks,
        IReadOnlyDictionary<Guid, IReadOnlyList<BacklogHealthCheck>> itemFlags,
        int readinessWindowItems,
        double? agingWipDays,
        double? oversizedEstimate)
    {
        Thresholds = thresholds;
        Checks = checks;
        ItemFlags = itemFlags;
        ReadinessWindowItems = readinessWindowItems;
        AgingWipDays = agingWipDays;
        OversizedEstimate = oversizedEstimate;
    }

    public BacklogHealthThresholds Thresholds { get; }

    /// <summary>
    /// One result per <see cref="BacklogHealthCheck"/>, in the enum's order.
    /// </summary>
    public IReadOnlyList<BacklogHealthCheckResult> Checks { get; }

    /// <summary>
    /// The checks that flagged each backlog item. Every item is present, unflagged ones with none.
    /// </summary>
    public IReadOnlyDictionary<Guid, IReadOnlyList<BacklogHealthCheck>> ItemFlags { get; }

    /// <summary>
    /// The number of top-ranked items the readiness checks looked at.
    /// </summary>
    public int ReadinessWindowItems { get; }

    /// <summary>
    /// The cycle time, in days, an active item is flagged beyond. Null without enough history.
    /// </summary>
    public double? AgingWipDays { get; }

    /// <summary>
    /// The estimate, in the team's sizing method, an item is flagged as oversized above. Null without enough
    /// history, or when the team sizes by count.
    /// </summary>
    public double? OversizedEstimate { get; }

    public BacklogHealthCheckResult this[BacklogHealthCheck check] => Checks.Single(c => c.Check == check);
}
