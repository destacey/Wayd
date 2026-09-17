using Wayd.Common.Application.Dtos;

namespace Wayd.ProductManagement.Application.DeliveryOverview.Dtos;

/// <summary>
/// Version activity over a window, for one product subtree or the whole catalog.
/// </summary>
/// <remarks>
/// Measured from versions rather than deployments, so it answers what was <em>cut and shipped</em>
/// rather than what reached an environment. The two are different questions and the deployment-based
/// measures live on their own page.
/// </remarks>
public sealed record DeliveryOverviewDto
{
    public DeliveryScopeDto Scope { get; init; } = default!;
    public ReleaseFrequencyDto Frequency { get; init; } = default!;
    public CutToReleasedDto CutToReleased { get; init; } = default!;

    /// <summary>
    /// The catalog subtree in scope, in reading order, one row per node.
    /// </summary>
    /// <remarks>
    /// Flattened depth-first rather than nested, so the view indents rather than grouping. Grouping
    /// rows under their parent's name duplicated any node that is both releasable and a parent: it
    /// appeared once as a row and again as a heading over its own children.
    /// <para>
    /// Carries the groupings a releasable node sits under, so the hierarchy reads, and every
    /// releasable node in scope whether or not it shipped anything — a product that shipped nothing
    /// is part of the answer.
    /// </para>
    /// </remarks>
    public IReadOnlyCollection<ProductActivityDto> Activity { get; init; } = [];
}

/// <summary>What the figures cover.</summary>
public sealed record DeliveryScopeDto
{
    /// <summary>The selected node, or <c>null</c> for the whole catalog.</summary>
    public NavigationDto? Product { get; init; }

    /// <summary>
    /// How many nodes in scope can have versions cut against them.
    /// </summary>
    /// <remarks>
    /// The denominator a reader needs to judge the activity below: three releases a week means
    /// something different across four products than across forty.
    /// </remarks>
    public int ReleasableNodeCount { get; init; }
}

/// <summary>How often versions shipped.</summary>
/// <remarks>
/// Carries its count and window alongside the rate so two windows can be combined by summing the
/// parts. Averaging the rates would weight a quiet week the same as a busy one.
/// </remarks>
public sealed record ReleaseFrequencyDto
{
    public int Count { get; init; }
    public double WindowDays { get; init; }
    public double PerWeek { get; init; }

    /// <summary>
    /// The same measure over the window immediately before this one, or <c>null</c> where nothing
    /// shipped then.
    /// </summary>
    /// <remarks>
    /// Null rather than zero: no prior activity means the change is unknowable, and rendering it as
    /// a rise from zero would invent a trend.
    /// </remarks>
    public double? PreviousPerWeek { get; init; }
}

/// <summary>How long a cut version waited before it shipped.</summary>
public sealed record CutToReleasedDto
{
    /// <summary>Mean days from cut to release, or <c>null</c> where nothing measurable shipped.</summary>
    public double? AverageDays { get; init; }

    /// <summary>
    /// How many of the window's releases could be measured.
    /// </summary>
    /// <remarks>
    /// A version can be released without ever being cut — imports and backfills do exactly that, and
    /// cutting is deliberately not a prerequisite. Those carry no latency and are excluded rather
    /// than counted as zero, which would make the average improve the more history you load. Showing
    /// the pair lets a reader see how much of the window the figure actually speaks for.
    /// </remarks>
    public int MeasuredCount { get; init; }

    /// <summary>Releases in the window, measurable or not.</summary>
    public int ReleasedCount { get; init; }

    /// <summary>The same mean over the window immediately before this one.</summary>
    public double? PreviousAverageDays { get; init; }
}

/// <summary>One node of the catalog subtree, with its releases day by day.</summary>
public sealed record ProductActivityDto
{
    public NavigationDto Product { get; init; } = default!;

    /// <summary>
    /// How far beneath the scope's root this node sits, so the view can indent without walking the
    /// tree a second time. Zero for a node at the top of the scope.
    /// </summary>
    public int Depth { get; init; }

    /// <summary>
    /// Whether versions can be cut against this node.
    /// </summary>
    /// <remarks>
    /// A grouping is present so the hierarchy reads, but it can never have releases of its own —
    /// drawing it an empty row of days would claim it had a quiet fortnight rather than that the
    /// question does not apply to it.
    /// </remarks>
    public bool IsReleasable { get; init; }

    /// <summary>Days with at least one release. A day with none is absent rather than zero.</summary>
    public IReadOnlyCollection<DailyReleaseCountDto> Days { get; init; } = [];

    /// <summary>The product's releases across the whole window.</summary>
    public int TotalReleased { get; init; }
}

/// <summary>Releases on one day.</summary>
public sealed record DailyReleaseCountDto
{
    public LocalDate Date { get; init; }
    public int Released { get; init; }

    /// <summary>
    /// How many of that day's versions have since been withdrawn.
    /// </summary>
    /// <remarks>
    /// A count, not a rate. Withdrawing a cut version is rare and drastic, so a percentage sits at
    /// zero for weeks and then reads as a trend on a single incident.
    /// </remarks>
    public int Withdrawn { get; init; }
}
