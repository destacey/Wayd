using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Roadmaps;

/// <summary>
/// A milestone was added to a roadmap.
/// </summary>
/// <remarks>
/// A milestone added beneath an activity can widen that activity's dates and its ancestors', recorded by the
/// <see cref="RoadmapItemDatesChangedEvent"/> raised alongside this one.
/// </remarks>
public sealed record RoadmapMilestoneAddedEvent : DomainEvent<RoadmapMilestoneAddedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public RoadmapMilestoneAddedEvent(
        Guid id,
        int key,
        Guid milestoneId,
        string name,
        string? description,
        Guid? parentId,
        string? color,
        LocalDate date,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        MilestoneId = milestoneId;
        Name = name;
        Description = description;
        ParentId = parentId;
        Color = color;
        Date = date;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid MilestoneId { get; }
    public string Name { get; }
    public string? Description { get; }

    /// <summary>The activity it sits beneath, or null at the roadmap's root.</summary>
    public Guid? ParentId { get; }

    public string? Color { get; }
    public LocalDate Date { get; }

    [JsonIgnore]
    public string AggregateType => "Roadmap";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
