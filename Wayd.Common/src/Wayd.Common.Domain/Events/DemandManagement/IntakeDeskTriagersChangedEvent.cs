using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// Who triages an intake desk's new requests changed: members started or stopped being told about requests
/// waiting on it unassigned.
/// </summary>
/// <remarks>
/// Carries the resulting triagers as well as the change, so the history reads without replaying it. A member who
/// leaves the desk stops triaging through <see cref="IntakeDeskMembersChangedEvent"/>, not this.
/// </remarks>
public sealed record IntakeDeskTriagersChangedEvent : DomainEvent<IntakeDeskTriagersChangedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public IntakeDeskTriagersChangedEvent(Guid id, int key, IReadOnlyList<Guid> added, IReadOnlyList<Guid> removed,
        IReadOnlyList<Guid> triagers, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Added = added;
        Removed = removed;
        Triagers = triagers;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The members who now triage the desk.</summary>
    public IReadOnlyList<Guid> Added { get; }

    /// <summary>The members who no longer triage it.</summary>
    public IReadOnlyList<Guid> Removed { get; }

    /// <summary>Everyone who triages the desk after the change.</summary>
    public IReadOnlyList<Guid> Triagers { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeDesk";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
