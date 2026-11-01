using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A record type was renamed.
/// </summary>
public sealed record RecordTypeRenamedEvent : DomainEvent<RecordTypeRenamedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public RecordTypeRenamedEvent(
        Guid id,
        int key,
        string ownerType,
        string name,
        string previousName,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        OwnerType = ownerType;
        Name = name;
        PreviousName = previousName;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string OwnerType { get; }
    public string Name { get; }

    /// <summary>The name this rename replaced.</summary>
    public string PreviousName { get; }

    [JsonIgnore]
    public string AggregateType => "RecordType";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
