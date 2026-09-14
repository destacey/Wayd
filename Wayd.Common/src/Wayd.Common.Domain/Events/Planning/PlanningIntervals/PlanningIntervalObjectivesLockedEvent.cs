using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.PlanningIntervals;

/// <summary>
/// A planning interval's objectives were locked: the plan is committed, so no objective can be added or deleted,
/// and each one's name and stretch flag are frozen.
/// </summary>
/// <remarks>
/// Recorded once, on the interval that holds the lock, rather than on every objective it covers.
/// </remarks>
public sealed record PlanningIntervalObjectivesLockedEvent : DomainEvent<PlanningIntervalObjectivesLockedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    [JsonConstructor]
    public PlanningIntervalObjectivesLockedEvent(Guid id, int key, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    [JsonIgnore]
    public string AggregateType => "PlanningInterval";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
