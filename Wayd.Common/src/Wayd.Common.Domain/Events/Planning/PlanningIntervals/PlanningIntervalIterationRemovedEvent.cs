using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.PlanningIntervals;

/// <summary>
/// An iteration was removed from a planning interval, along with the sprints mapped to it.
/// </summary>
/// <remarks>
/// The name is carried because the iteration is gone by the time anyone reads the entry.
/// </remarks>
public sealed record PlanningIntervalIterationRemovedEvent : DomainEvent<PlanningIntervalIterationRemovedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public PlanningIntervalIterationRemovedEvent(
        Guid id,
        int key,
        Guid iterationId,
        string name,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        IterationId = iterationId;
        Name = name;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid IterationId { get; }
    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "PlanningInterval";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
