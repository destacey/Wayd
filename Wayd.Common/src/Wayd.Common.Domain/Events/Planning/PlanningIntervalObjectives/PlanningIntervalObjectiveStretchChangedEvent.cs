using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.PlanningIntervalObjectives;

/// <summary>
/// A planning interval objective became a stretch objective, or became committed.
/// </summary>
/// <remarks>
/// Kept apart from the details because it changes what the objective counts for: a stretch objective is left
/// out of the predictability a team is measured against. The flag before the change is the opposite of
/// <see cref="IsStretch"/>.
/// </remarks>
public sealed record PlanningIntervalObjectiveStretchChangedEvent : DomainEvent<PlanningIntervalObjectiveStretchChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public PlanningIntervalObjectiveStretchChangedEvent(
        Guid id,
        int key,
        bool isStretch,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        IsStretch = isStretch;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public bool IsStretch { get; }

    [JsonIgnore]
    public string AggregateType => "PlanningIntervalObjective";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
