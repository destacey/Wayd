using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Roadmaps;

/// <summary>
/// The dates of one or more roadmap items moved.
/// </summary>
/// <remarks>
/// One change to an item's dates can move others with it: moving an activity without changing its length shifts
/// everything beneath it, and an item that outgrows its parent widens every ancestor. All of them are one
/// occurrence, so they are carried together, the item that was changed first.
/// </remarks>
public sealed record RoadmapItemDatesChangedEvent : DomainEvent<RoadmapItemDatesChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.ScheduleChanged;

    [JsonConstructor]
    public RoadmapItemDatesChangedEvent(
        Guid id,
        int key,
        RoadmapItemDateChange[] changes,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Changes = [.. changes];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>Every item whose dates moved, with both ends.</summary>
    public RoadmapItemDateChange[] Changes { get; }

    [JsonIgnore]
    public string AggregateType => "Roadmap";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
