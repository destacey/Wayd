using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.Common.Domain.Events.ProductManagement.Incidents;

/// <summary>An incident was opened.</summary>
public sealed record IncidentCreatedEvent : DomainEvent<IncidentCreatedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public IncidentCreatedEvent(
        Guid id,
        int key,
        string title,
        Guid commanderEmployeeId,
        Guid severityLevelId,
        Instant noticedAt,
        Instant? startedAt,
        Instant? recoveredAt,
        ProductStatusAlias statusAlias,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Title = title;
        CommanderEmployeeId = commanderEmployeeId;
        SeverityLevelId = severityLevelId;
        NoticedAt = noticedAt;
        StartedAt = startedAt;
        RecoveredAt = recoveredAt;
        StatusAlias = statusAlias;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Title { get; }
    public Guid CommanderEmployeeId { get; }
    public Guid SeverityLevelId { get; }
    public Instant NoticedAt { get; }
    public Instant? StartedAt { get; }
    public Instant? RecoveredAt { get; }
    public ProductStatusAlias StatusAlias { get; }

    [JsonIgnore]
    public string AggregateType => "Incident";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
