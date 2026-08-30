namespace Wayd.ProductManagement.Application.DeliveryMetrics.Dtos;

/// <summary>
/// The delivery measures over one window.
/// </summary>
/// <remarks>
/// Two of the four DORA measures are computable from what this module records. The other two are
/// carried as <see cref="UnavailableMetricDto"/> rather than omitted or approximated, so a caller can
/// tell "we do not measure this yet" from "there were no deployments" — see
/// <see cref="Unavailable"/> for why each is missing.
/// <para>
/// Counts and totals are carried alongside every average so a caller can combine windows or scopes
/// without average-of-averages bias, matching <c>CycleTimeSummary</c> in Work.
/// </para>
/// </remarks>
public sealed record DeliveryMetricsDto
{
    /// <summary>The window these measures cover, inclusive of both ends.</summary>
    public required Instant From { get; init; }

    /// <summary>The end of the window.</summary>
    public required Instant To { get; init; }

    public required DeploymentFrequencyDto DeploymentFrequency { get; init; }

    public required ChangeFailureRateDto ChangeFailureRate { get; init; }

    /// <summary>
    /// The measures this module cannot compute yet, each with the reason.
    /// </summary>
    public IReadOnlyCollection<UnavailableMetricDto> Unavailable { get; init; } = [];
}

/// <summary>
/// How often production deployments completed over the window.
/// </summary>
/// <remarks>
/// Counts completed deployments, not attempts: an in-flight deployment has not been delivered, and a
/// failed one did not reach production. Rolled-back deployments <em>do</em> count — they reached
/// production, which is what this measures; whether that was a good idea is change failure rate's job.
/// </remarks>
public sealed record DeploymentFrequencyDto
{
    /// <summary>Production deployments that reached their environment in the window.</summary>
    public required int Count { get; init; }

    /// <summary>Days the window spans, so a caller can recompute the rate over any period.</summary>
    public required double WindowDays { get; init; }

    /// <summary>
    /// Deployments per day, or <c>null</c> when the window has no duration. Derived — a caller
    /// combining windows should sum <see cref="Count"/> and <see cref="WindowDays"/> instead.
    /// </summary>
    public double? PerDay => WindowDays > 0 ? Count / WindowDays : null;
}

/// <summary>
/// The share of production deployments that failed or were rolled back.
/// </summary>
/// <remarks>
/// Only production counts. A failure caught in staging is a failure that was prevented, and counting it
/// would invert what the measure means.
/// </remarks>
public sealed record ChangeFailureRateDto
{
    /// <summary>Completed production deployments in the window — the denominator.</summary>
    public required int TotalDeployments { get; init; }

    /// <summary>Those that failed or were rolled back — the numerator.</summary>
    public required int FailedDeployments { get; init; }

    /// <summary>
    /// The failure share from 0 to 1, or <c>null</c> when nothing deployed. Null is "no deployments to
    /// judge", which is not the same as a rate of zero.
    /// </summary>
    public double? Rate => TotalDeployments > 0 ? (double)FailedDeployments / TotalDeployments : null;
}

/// <summary>
/// A measure this module does not compute yet, and why.
/// </summary>
/// <remarks>
/// Reported rather than omitted so a caller can show the gap honestly instead of leaving a reader to
/// guess whether the number is zero, still loading, or not collected. Deliberately not behind a feature
/// flag: a flag says "built but switched off", and these are not built.
/// </remarks>
public sealed record UnavailableMetricDto
{
    /// <summary>The measure's name, as a reader would recognise it.</summary>
    public required string Metric { get; init; }

    /// <summary>What is missing before it can be computed.</summary>
    public required string Reason { get; init; }
}
