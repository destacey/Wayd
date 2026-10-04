using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Roadmaps;

/// <summary>
/// A roadmap item's name, description or color was edited.
/// </summary>
public sealed record RoadmapItemDetailsUpdatedEvent : DomainEvent<RoadmapItemDetailsUpdatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public RoadmapItemDetailsUpdatedEvent(
        Guid id,
        int key,
        Guid itemId,
        RoadmapItemType itemType,
        string name,
        string? description,
        string? color,
        RoadmapItemDetails previous,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        ItemId = itemId;
        ItemType = itemType;
        Name = name;
        Description = description;
        Color = color;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ItemId { get; }
    public RoadmapItemType ItemType { get; }
    public string Name { get; }
    public string? Description { get; }
    public string? Color { get; }

    /// <summary>The details this edit replaced.</summary>
    public RoadmapItemDetails Previous { get; }

    [JsonIgnore]
    public string AggregateType => "Roadmap";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
