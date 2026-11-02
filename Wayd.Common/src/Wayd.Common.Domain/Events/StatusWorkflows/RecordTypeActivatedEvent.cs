using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A record type can be given to records again.
/// </summary>
public sealed record RecordTypeActivatedEvent : DomainEvent<RecordTypeActivatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    [JsonConstructor]
    public RecordTypeActivatedEvent(Guid id, int key, string ownerType, EventActor actor, Instant timestamp)
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
