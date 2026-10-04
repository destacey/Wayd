using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Roadmaps;

/// <summary>
/// The activities beneath one parent were put in a different order.
/// </summary>
public sealed record RoadmapActivitiesReorderedEvent : DomainEvent<RoadmapActivitiesReorderedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public RoadmapActivitiesReorderedEvent(
        Guid id,
        int key,
        Guid? parentId,
        Guid[] previousOrder,
        Guid[] order,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        ParentId = parentId;
        PreviousOrder = [.. previousOrder];
        Order = [.. order];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The activity whose children were reordered, or null for the roadmap's root.</summary>
    public Guid? ParentId { get; }

    /// <summary>The activity ids in their order before the change.</summary>
    public Guid[] PreviousOrder { get; }

    /// <summary>The activity ids in their order after the change.</summary>
    public Guid[] Order { get; }

    [JsonIgnore]
    public string AggregateType => "Roadmap";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
