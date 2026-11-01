using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A record type can no longer be given to records. Records that have it keep it.
/// </summary>
public sealed record RecordTypeDeactivatedEvent : DomainEvent<RecordTypeDeactivatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    [JsonConstructor]
    public RecordTypeDeactivatedEvent(Guid id, int key, string ownerType, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        OwnerType = ownerType;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string OwnerType { get; }

    [JsonIgnore]
    public string AggregateType => "RecordType";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
