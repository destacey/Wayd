using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement.Incidents;

/// <summary>An incident's noticed, response or recovery times changed.</summary>
public sealed record IncidentTimelineChangedEvent : DomainEvent<IncidentTimelineChangedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.ScheduleChanged;

    [JsonConstructor]
    public IncidentTimelineChangedEvent(
        Guid id,
        int key,
        Instant previousNoticedAt,
        Instant? previousStartedAt,
        Instant? previousRecoveredAt,
        Instant noticedAt,
        Instant? startedAt,
        Instant? recoveredAt,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PreviousNoticedAt = previousNoticedAt;
        PreviousStartedAt = previousStartedAt;
        PreviousRecoveredAt = previousRecoveredAt;
        NoticedAt = noticedAt;
        StartedAt = startedAt;
        RecoveredAt = recoveredAt;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Instant PreviousNoticedAt { get; }
    public Instant? PreviousStartedAt { get; }
    public Instant? PreviousRecoveredAt { get; }
    public Instant NoticedAt { get; }
    public Instant? StartedAt { get; }
    public Instant? RecoveredAt { get; }

    [JsonIgnore]
    public string AggregateType => "Incident";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
