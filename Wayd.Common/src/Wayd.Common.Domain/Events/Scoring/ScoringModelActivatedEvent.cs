using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Scoring;

/// <summary>
/// A proposed scoring model was activated, locking its shape and making it assignable.
/// </summary>
/// <remarks>
/// Only a proposed model can be activated, so the type records both ends of the transition.
/// </remarks>
public sealed record ScoringModelActivatedEvent : DomainEvent<ScoringModelActivatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    [JsonConstructor]
    public ScoringModelActivatedEvent(Guid id, int key, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    [JsonIgnore]
    public string AggregateType => "ScoringModel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
