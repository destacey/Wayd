using System.Text.Json.Serialization;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Roadmaps;

/// <summary>
/// An activity was added to a roadmap.
/// </summary>
/// <remarks>
/// An activity added beneath another can widen its ancestors' dates, recorded by the
/// <see cref="RoadmapItemDatesChangedEvent"/> raised alongside this one.
/// </remarks>
public sealed record RoadmapActivityAddedEvent : DomainEvent<RoadmapActivityAddedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public RoadmapActivityAddedEvent(
        Guid id,
        int key,
        Guid activityId,
        string name,
        string? description,
        Guid? parentId,
        string? color,
        LocalDateRange dateRange,
        int order,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        ActivityId = activityId;
        Name = name;
        Description = description;
        ParentId = parentId;
        Color = color;
        DateRange = dateRange;
        Order = order;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ActivityId { get; }
    public string Name { get; }
    public string? Description { get; }

    /// <summary>The activity it sits beneath, or null at the roadmap's root.</summary>
    public Guid? ParentId { get; }

    public string? Color { get; }
    public LocalDateRange DateRange { get; }

    /// <summary>Its position among its sibling activities.</summary>
    public int Order { get; }

    [JsonIgnore]
    public string AggregateType => "Roadmap";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
