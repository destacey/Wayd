using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.PlanningIntervalObjectives;

/// <summary>
/// A planning interval objective moved to a different position among its interval's objectives.
/// </summary>
/// <remarks>
/// Raised on each objective whose position changed, because the position is the objective's own state. A
/// reorder that moves one objective past three others records four of these.
/// </remarks>
public sealed record PlanningIntervalObjectiveOrderChangedEvent : DomainEvent<PlanningIntervalObjectiveOrderChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public PlanningIntervalObjectiveOrderChangedEvent(
        Guid id,
        int key,
        int? previousOrder,
        int? order,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PreviousOrder = previousOrder;
        Order = order;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public int? PreviousOrder { get; }
    public int? Order { get; }

    [JsonIgnore]
    public string AggregateType => "PlanningIntervalObjective";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
