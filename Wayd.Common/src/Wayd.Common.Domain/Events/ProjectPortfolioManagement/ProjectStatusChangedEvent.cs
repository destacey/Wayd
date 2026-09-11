using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A project moved to a different status.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="ProjectStatusChangedEventV2"/> replaced it.
/// Kept so every payload written as this type still deserializes into it — its name and members are the
/// contract those payloads were written against, so neither may change.
/// <para>
/// The <c>Backfill-Project-Status-Activity</c> migration writes its rows as this type, so this is the
/// shape those rows must keep deserializing into.
/// </para>
/// </remarks>
[Obsolete("Superseded by ProjectStatusChangedEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record ProjectStatusChangedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectStatusChangedEvent(
        Guid statusHistoryId,
        Guid id,
        ProjectKey key,
        string name,
        string? fromStatus,
        LifecycleCategory? fromCategory,
        string toStatus,
        LifecycleCategory toCategory,
        bool isBackward,
        string? reason,
        int sequence,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        EventId = statusHistoryId;

        Id = id;
        Key = key;
        Name = name;
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
    public string Name { get; }

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
