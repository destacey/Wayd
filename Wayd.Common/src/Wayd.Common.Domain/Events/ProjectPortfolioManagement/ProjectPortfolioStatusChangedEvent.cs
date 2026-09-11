using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A portfolio moved to a different status.
/// </summary>
/// <remarks>
/// One event covers activation, pausing, resuming, closing and archiving rather than a type per verb,
/// matching <see cref="ProjectStatusChangedEvent"/> — see its remarks for why.
/// <para>
/// <see cref="ToCategory"/> does not separate Active from On Hold, and <see cref="ToStatus"/> is what
/// distinguishes them: both statuses are lifecycle-Active, since a paused portfolio is still running work.
/// </para>
/// <para>
/// Carries <see cref="DateRange"/> because a portfolio's dates only move as part of a transition —
/// activating sets the start and closing sets the end — so there is no separate timeline event to read
/// them from.
/// </para>
/// <para>
/// A portfolio keeps no status history, so this event is the only record a transition leaves: nothing
/// existed before it to share an id with, and nothing can be backfilled from.
/// </para>
/// </remarks>
public sealed record ProjectPortfolioStatusChangedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectPortfolioStatusChangedEvent(
        Guid id,
        int key,
        string fromStatus,
        LifecycleCategory fromCategory,
        string toStatus,
        LifecycleCategory toCategory,
        FlexibleDateRange? dateRange,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        FromStatus = fromStatus;
        FromCategory = fromCategory;
        ToStatus = toStatus;
        ToCategory = toCategory;
        DateRange = dateRange;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    public string FromStatus { get; }
    public LifecycleCategory FromCategory { get; }
    public string ToStatus { get; }
    public LifecycleCategory ToCategory { get; }

    /// <summary>The portfolio's dates after the transition, or null while it is still Proposed.</summary>
    public FlexibleDateRange? DateRange { get; }

    [JsonIgnore]
    public string AggregateType => "ProjectPortfolio";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
