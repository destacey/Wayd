using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.PlanningIntervalObjectives;

/// <summary>
/// A health check was recorded against a planning interval objective.
/// </summary>
public sealed record PlanningIntervalObjectiveHealthCheckAddedEvent : DomainEvent<PlanningIntervalObjectiveHealthCheckAddedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Health;

    [JsonConstructor]
    public PlanningIntervalObjectiveHealthCheckAddedEvent(
        Guid id,
        int key,
        Guid healthCheckId,
        HealthStatus status,
        string? note,
        Instant expiration,
        Guid reportedById,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        HealthCheckId = healthCheckId;
        Status = status;
        Note = note;
        Expiration = expiration;
        ReportedById = reportedById;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The health check this event is about.</summary>
    public Guid HealthCheckId { get; }

    public HealthStatus Status { get; }
    public string? Note { get; }

    /// <summary>When the check stops being current.</summary>
    public Instant Expiration { get; }

    /// <summary>The employee who reported it, frozen here as it is on the check itself.</summary>
    public Guid ReportedById { get; }

    [JsonIgnore]
    public string AggregateType => "PlanningIntervalObjective";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
