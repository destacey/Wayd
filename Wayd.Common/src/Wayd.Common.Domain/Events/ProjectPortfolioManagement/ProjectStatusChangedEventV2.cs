using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A project moved to a different status.
/// </summary>
/// <remarks>
/// <para>
/// One event covers every transition — approval, activation, completion, cancellation and reversal —
/// rather than a type per verb. A fixed set of verbs presumes fixed statuses, and PPM statuses are on
/// their way to being workflow-configurable; a project moving to a status an organization invented would
/// then raise nothing at all, which is worse than a shared type. <see cref="ToCategory"/> answers the
/// coarse question without knowing the workflow, so a consumer that only cares whether work started or
/// finished need not learn every status.
/// </para>
/// <para>
/// This event carries both ends because a transition <em>is</em> the pair: the history it mirrors is
/// keyed on movement, and "reverted from Active" cannot be recovered from the new status alone. Events
/// that record a new state rather than a movement carry only the new value and leave deltas to comparison.
/// </para>
/// <para>
/// <see cref="DomainEvent.EventId"/> is the id of the <c>ProjectStatusHistory</c> row this transition
/// wrote, not a fresh value. The two records describe one fact, so sharing an identity is what lets
/// history recorded before this event existed be replayed into the activity log exactly once, and lets
/// that replay run again without duplicating anything.
/// </para>
/// <para>
/// Supersedes <see cref="ProjectStatusChangedEvent"/>, dropping its required <c>Name</c>, which described the
/// project rather than the change. A new type rather than a new version, because removing a required member
/// breaks every consumer written against the old shape.
/// </para>
/// </remarks>
public sealed record ProjectStatusChangedEventV2 : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectStatusChangedEventV2(
        Guid statusHistoryId,
        Guid id,
        ProjectKey key,
        string? fromStatus,
        LifecycleCategory? fromCategory,
        string toStatus,
        LifecycleCategory toCategory,
        bool isBackward,
        string? reason,
        int sequence,
        EventActor actor,
        Instant timestamp)
        : base(actor, "2.0")
    {
        EventId = statusHistoryId;

        Id = id;
        Key = key;
        FromStatus = fromStatus;
        FromCategory = fromCategory;
        ToStatus = toStatus;
        ToCategory = toCategory;
        IsBackward = isBackward;
        Reason = reason;
        Sequence = sequence;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }

    /// <summary>
    /// The status the project moved out of, or null when this records the project entering its initial
    /// state.
    /// </summary>
    public string? FromStatus { get; }

    public LifecycleCategory? FromCategory { get; }

    public string ToStatus { get; }

    /// <summary>
    /// The lifecycle position the project moved into. This is what a consumer outside PPM branches on —
    /// never the status name, which a workflow administrator may rename.
    /// </summary>
    public LifecycleCategory ToCategory { get; }

    /// <summary>
    /// Whether this moved the project back to an earlier status. Carried rather than derived because the
    /// table that decides it lives in the PPM domain, out of reach of the consumers that read this event.
    /// </summary>
    public bool IsBackward { get; }

    /// <summary>
    /// Why the project was moved. Required for a backward transition and null for the forward ones,
    /// which are explained by the move itself.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// The project's monotonic transition number. The only reliable ordering for a project's status
    /// history: several transitions can share a timestamp, and a reverted project enters a status twice.
    /// </summary>
    public int Sequence { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
