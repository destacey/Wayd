using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.PlanningIntervalObjectives;

/// <summary>
/// An existing health check on a planning interval objective was corrected.
/// </summary>
/// <remarks>
/// Carries what the check said before as well as after, because the correction is the fact: a consumer that
/// acted on "Healthy" needs to know that is what was withdrawn.
/// </remarks>
public sealed record PlanningIntervalObjectiveHealthCheckUpdatedEvent : DomainEvent<PlanningIntervalObjectiveHealthCheckUpdatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Health;

    [JsonConstructor]
    public PlanningIntervalObjectiveHealthCheckUpdatedEvent(
        Guid id,
        int key,
        Guid healthCheckId,
        HealthStatus previousStatus,
        string? previousNote,
        Instant previousExpiration,
        HealthStatus status,
        string? note,
        Instant expiration,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        HealthCheckId = healthCheckId;
        PreviousStatus = previousStatus;
        PreviousNote = previousNote;
        PreviousExpiration = previousExpiration;
        Status = status;
        Note = note;
        Expiration = expiration;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The health check this event is about.</summary>
    public Guid HealthCheckId { get; }

    public HealthStatus PreviousStatus { get; }
    public string? PreviousNote { get; }
    public Instant PreviousExpiration { get; }

    public HealthStatus Status { get; }
    public string? Note { get; }
    public Instant Expiration { get; }

    [JsonIgnore]
    public string AggregateType => "PlanningIntervalObjective";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
