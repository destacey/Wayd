using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A record type was defined in Wayd, or first recorded from its source.
/// </summary>
public sealed record RecordTypeCreatedEvent : DomainEvent<RecordTypeCreatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public RecordTypeCreatedEvent(
        Guid id,
        int key,
        string ownerType,
        string name,
        WorkflowOwnership ownership,
        string? externalKey,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        OwnerType = ownerType;
        Name = name;
        Ownership = ownership;
        ExternalKey = externalKey;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The owner type it is a variant of, fixed for the type's life.</summary>
    public string OwnerType { get; }

    public string Name { get; }

    /// <summary>Whether administrators keep it or a sync does.</summary>
    public WorkflowOwnership Ownership { get; }

    /// <summary>The source's identifier for a managed type; null for an owned one.</summary>
    public string? ExternalKey { get; }

    [JsonIgnore]
    public string AggregateType => "RecordType";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
