using Wayd.Common.Domain.Enums.Organization;
using Wayd.Work.Domain.Models.SprintScope;

namespace Wayd.Work.Application.WorkItems.Dtos;

/// <summary>
/// What a sprint committed to and what became of it, worked out from work item history between the sprint's
/// effective start and end. Only requirement-tier work counts.
/// </summary>
public sealed record SprintScopeDto
{
    public Guid SprintId { get; init; }

    /// <summary>
    /// The estimate the report is measured in: its team's sizing method on the sprint's planned start, or
    /// Count for a sprint with no team. Under Count every estimate equals its item count.
    /// </summary>
    public SizingMethod SizingMethod { get; init; }

    /// <summary>The commitment point: what was in the sprint here is committed.</summary>
    public Instant EffectiveStart { get; init; }

    /// <summary>Whether the effective start is the team's recorded start rather than the default.</summary>
    public bool StartIsActual { get; init; }

    /// <summary>The effective end: what is in the sprint here is completed or carried over.</summary>
    public Instant EffectiveEnd { get; init; }

    /// <summary>Whether the effective end is the team's recorded completion rather than the default.</summary>
    public bool EndIsActual { get; init; }

    /// <summary>
    /// The start of the sprint's last day. Unfinished work moved to the team's next sprint from here on is
    /// carried over; before it, descoped.
    /// </summary>
    public Instant LastDay { get; init; }

    /// <summary>The IANA zone the sprint's days are counted in.</summary>
    public required string TimeZone { get; init; }

    /// <summary>
    /// Whether the sprint has a team. A sprint with none is counted in the system default zone and grace
    /// period, by item count.
    /// </summary>
    public bool HasTeam { get; init; }

    /// <summary>
    /// Whether the history of a workspace holding the sprint's work has not yet been read through to the end.
    /// The figures are then unreliable, and a full sync completes them.
    /// </summary>
    public bool HistoryIncomplete { get; init; }

    public required SprintScopeTotalsDto Totals { get; init; }

    public required List<SprintScopeItemDto> Items { get; init; }
}

/// <summary>How many items, and how much estimate, fall in one category. A missing estimate adds nothing.</summary>
public sealed record SprintScopeMeasureDto(int Count, double Estimate)
{
    public static SprintScopeMeasureDto From(SprintScopeMeasure measure) => new(measure.Count, measure.Estimate);
}

/// <summary>
/// A sprint's scope summed by category. Committed and Added are measured with the estimate each item had when
/// it came in; the outcomes with the estimate it had when it was last in the sprint.
/// </summary>
public sealed record SprintScopeTotalsDto
{
    /// <summary>Every item that was in scope: committed and added.</summary>
    public required SprintScopeMeasureDto Total { get; init; }

    public required SprintScopeMeasureDto Committed { get; init; }

    public required SprintScopeMeasureDto Added { get; init; }

    /// <summary>Completed items, including those completed as Removed.</summary>
    public required SprintScopeMeasureDto Completed { get; init; }

    /// <summary>The part of <see cref="Completed"/> completed as Removed.</summary>
    public required SprintScopeMeasureDto Removed { get; init; }

    public required SprintScopeMeasureDto CarriedOver { get; init; }

    public required SprintScopeMeasureDto Descoped { get; init; }

    /// <summary>
    /// Committed items that were completed, measured with their committed estimate so it compares with
    /// <see cref="Committed"/>.
    /// </summary>
    public required SprintScopeMeasureDto CompletedOfCommitted { get; init; }

    /// <summary>Completed of committed ÷ committed, by item count; null when nothing was committed.</summary>
    public double? SayDoCount { get; init; }

    /// <summary>Completed of committed ÷ committed, by estimate; null when the committed work had no estimate.</summary>
    public double? SayDoEstimate { get; init; }

    /// <summary>Items with no value in the sprint's estimate when they were last in the sprint.</summary>
    public int Unestimated { get; init; }

    public static SprintScopeTotalsDto From(SprintScopeTotals totals) => new()
    {
        Total = SprintScopeMeasureDto.From(totals.Total),
        Committed = SprintScopeMeasureDto.From(totals.Committed),
        Added = SprintScopeMeasureDto.From(totals.Added),
        Completed = SprintScopeMeasureDto.From(totals.Completed),
        Removed = SprintScopeMeasureDto.From(totals.Removed),
        CarriedOver = SprintScopeMeasureDto.From(totals.CarriedOver),
        Descoped = SprintScopeMeasureDto.From(totals.Descoped),
        CompletedOfCommitted = SprintScopeMeasureDto.From(totals.CompletedOfCommitted),
        SayDoCount = totals.SayDoCount,
        SayDoEstimate = totals.SayDoEstimate,
        Unestimated = totals.Unestimated,
    };
}

/// <summary>A work item that was in the sprint's scope, as it is now, with how it came in and what became of it.</summary>
public sealed record SprintScopeItemDto
{
    /// <summary>The work item as it is now; its current sprint may be another one.</summary>
    public required SprintBacklogItemDto WorkItem { get; init; }

    public SprintScopeEntry Entry { get; init; }

    public SprintScopeOutcome Outcome { get; init; }

    /// <summary>When an added item entered the sprint; null for a committed one.</summary>
    public Instant? EnteredAt { get; init; }

    /// <summary>When the item last left the sprint; null if it was still in at the effective end.</summary>
    public Instant? LeftAt { get; init; }

    /// <summary>The item's estimate when it was committed or added.</summary>
    public double? EntryEstimate { get; init; }

    /// <summary>The item's estimate when it was last in the sprint.</summary>
    public double? OutcomeEstimate { get; init; }
}
