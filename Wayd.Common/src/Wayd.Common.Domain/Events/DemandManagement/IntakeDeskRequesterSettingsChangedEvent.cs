using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// What requesters may do on an intake desk changed: whether they can choose it, or whether they may add
/// participants on it while it is private.
/// </summary>
public sealed record IntakeDeskRequesterSettingsChangedEvent : DomainEvent<IntakeDeskRequesterSettingsChangedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public IntakeDeskRequesterSettingsChangedEvent(Guid id, int key, bool isRequesterSelectable, bool requesterMayAddParticipants,
        bool previousIsRequesterSelectable, bool previousRequesterMayAddParticipants, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        IsRequesterSelectable = isRequesterSelectable;
        RequesterMayAddParticipants = requesterMayAddParticipants;
        PreviousIsRequesterSelectable = previousIsRequesterSelectable;
        PreviousRequesterMayAddParticipants = previousRequesterMayAddParticipants;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public bool IsRequesterSelectable { get; }
    public bool RequesterMayAddParticipants { get; }
    public bool PreviousIsRequesterSelectable { get; }
    public bool PreviousRequesterMayAddParticipants { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeDesk";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
