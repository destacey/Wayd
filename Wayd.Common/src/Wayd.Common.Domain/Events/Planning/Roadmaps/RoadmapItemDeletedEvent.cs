using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Roadmaps;

/// <summary>
/// An item was deleted from a roadmap, with everything beneath it.
/// </summary>
/// <remarks>
/// The name is carried because the item is gone by the time anyone reads the entry.
/// </remarks>
public sealed record RoadmapItemDeletedEvent : DomainEvent<RoadmapItemDeletedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public RoadmapItemDeletedEvent(
        Guid id,
        int key,
        Guid itemId,
        RoadmapItemType itemType,
        string name,
        RoadmapItemReference[] descendants,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        ItemId = itemId;
        ItemType = itemType;
        Name = name;
        Descendants = [.. descendants];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ItemId { get; }
    public RoadmapItemType ItemType { get; }
    public string Name { get; }

    /// <summary>The items beneath a deleted activity, deleted with it.</summary>
    public RoadmapItemReference[] Descendants { get; }

    [JsonIgnore]
    public string AggregateType => "Roadmap";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
