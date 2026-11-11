using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.Common.Domain.Events.ProductManagement.Incidents;

/// <summary>An incident was opened.</summary>
/// <remarks>
/// <see cref="Symptoms"/> and <see cref="RootCause"/> were added after the first payloads. A payload
/// written before them deserializes both as null, which is what an incident opened with neither recorded.
/// </remarks>
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
        Instant timestamp,
        string? symptoms = null,
        string? rootCause = null)
        : base(actor, "1.1")
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
        Symptoms = symptoms;
        RootCause = rootCause;

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

    /// <summary>What responders were seeing when the incident was opened. Null when nothing was recorded.</summary>
    public string? Symptoms { get; }

    /// <summary>Why it happened, if that was already known. Null until a cause is recorded.</summary>
    public string? RootCause { get; }

    [JsonIgnore]
    public string AggregateType => "Incident";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
