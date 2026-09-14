using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.PlanningIntervalObjectives;

/// <summary>
/// A planning interval objective was deleted.
/// </summary>
/// <remarks>
/// The name is carried because the record it describes is gone by the time anyone reads the entry.
/// </remarks>
public sealed record PlanningIntervalObjectiveDeletedEvent : DomainEvent<PlanningIntervalObjectiveDeletedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    [JsonConstructor]
    public PlanningIntervalObjectiveDeletedEvent(
        Guid id,
        int key,
        Guid planningIntervalId,
        string name,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PlanningIntervalId = planningIntervalId;
        Name = name;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid PlanningIntervalId { get; }
    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "PlanningIntervalObjective";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
