using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Roadmaps;

/// <summary>
/// A roadmap was made public, or private to its managers.
/// </summary>
public sealed record RoadmapVisibilityChangedEvent : DomainEvent<RoadmapVisibilityChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public RoadmapVisibilityChangedEvent(
        Guid id,
        int key,
        Visibility previousVisibility,
        Visibility visibility,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PreviousVisibility = previousVisibility;
        Visibility = visibility;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Visibility PreviousVisibility { get; }
    public Visibility Visibility { get; }

    [JsonIgnore]
    public string AggregateType => "Roadmap";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
