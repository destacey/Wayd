using System.Text.Json.Serialization;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Roadmaps;

/// <summary>
/// A timebox was added to a roadmap.
/// </summary>
/// <remarks>
/// A timebox added beneath an activity can widen that activity's dates and its ancestors', recorded by the
/// <see cref="RoadmapItemDatesChangedEvent"/> raised alongside this one.
/// </remarks>
public sealed record RoadmapTimeboxAddedEvent : DomainEvent<RoadmapTimeboxAddedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public RoadmapTimeboxAddedEvent(
        Guid id,
        int key,
        Guid timeboxId,
        string name,
        string? description,
        Guid? parentId,
        string? color,
        LocalDateRange dateRange,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        TimeboxId = timeboxId;
        Name = name;
        Description = description;
        ParentId = parentId;
        Color = color;
        DateRange = dateRange;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid TimeboxId { get; }
    public string Name { get; }
    public string? Description { get; }

    /// <summary>The activity it sits beneath, or null at the roadmap's root.</summary>
    public Guid? ParentId { get; }

    public string? Color { get; }
    public LocalDateRange DateRange { get; }

    [JsonIgnore]
    public string AggregateType => "Roadmap";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
