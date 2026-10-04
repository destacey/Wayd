using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Roadmaps;

/// <summary>
/// An archived roadmap was activated again.
/// </summary>
public sealed record RoadmapActivatedEvent : DomainEvent<RoadmapActivatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    [JsonConstructor]
    public RoadmapActivatedEvent(
        Guid id,
        int key,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    [JsonIgnore]
    public string AggregateType => "Roadmap";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
