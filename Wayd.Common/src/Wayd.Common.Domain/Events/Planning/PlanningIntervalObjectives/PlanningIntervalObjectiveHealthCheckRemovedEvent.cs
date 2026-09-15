using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.PlanningIntervalObjectives;

/// <summary>
/// A health check was removed from a planning interval objective.
/// </summary>
/// <remarks>
/// Removing a check can change the objective's reported health, by uncovering an older one or leaving none at
/// all, so it is a fact in its own right rather than the mere absence of one.
/// </remarks>
public sealed record PlanningIntervalObjectiveHealthCheckRemovedEvent : DomainEvent<PlanningIntervalObjectiveHealthCheckRemovedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Health;

    [JsonConstructor]
    public PlanningIntervalObjectiveHealthCheckRemovedEvent(
        Guid id,
        int key,
        Guid healthCheckId,
        HealthStatus status,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        HealthCheckId = healthCheckId;
        Status = status;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The health check this event is about.</summary>
    public Guid HealthCheckId { get; }

    /// <summary>The status the removed check was carrying, so the entry says what was taken away.</summary>
    public HealthStatus Status { get; }

    [JsonIgnore]
    public string AggregateType => "PlanningIntervalObjective";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
