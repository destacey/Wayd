using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Events.ProductManagement.Incidents;

/// <summary>An incident moved to a different status.</summary>
public sealed record IncidentStatusChangedEvent : DomainEvent<IncidentStatusChangedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StatusChanged;

    [JsonConstructor]
    public IncidentStatusChangedEvent(
        Guid id,
        int key,
        Guid fromStatusId,
        StatusCategory fromCategory,
        ProductStatusAlias fromAlias,
        Guid toStatusId,
        StatusCategory toCategory,
        ProductStatusAlias toAlias,
        EventActor actor,
        Instant timestamp,
        Instant? fromStartedAt,
        Instant? toStartedAt,
        Instant? fromRecoveredAt,
        Instant? toRecoveredAt)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        FromStatusId = fromStatusId;
        FromCategory = fromCategory;
        FromAlias = fromAlias;
        ToStatusId = toStatusId;
        ToCategory = toCategory;
        ToAlias = toAlias;
        FromStartedAt = fromStartedAt;
        ToStartedAt = toStartedAt;
        FromRecoveredAt = fromRecoveredAt;
        ToRecoveredAt = toRecoveredAt;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    public Guid FromStatusId { get; }
    public StatusCategory FromCategory { get; }
    public ProductStatusAlias FromAlias { get; }

    public Guid ToStatusId { get; }
    public StatusCategory ToCategory { get; }
    public ProductStatusAlias ToAlias { get; }

    /// <summary>When response had started before this move. Null when the move did not record it.</summary>
    public Instant? FromStartedAt { get; }

    /// <summary>When response starts after this move. Null when the move did not record it.</summary>
    public Instant? ToStartedAt { get; }

    /// <summary>When service had been restored before this move. Null when the move did not record it.</summary>
    public Instant? FromRecoveredAt { get; }

    /// <summary>When service is restored after this move. Null when the move did not record it.</summary>
    public Instant? ToRecoveredAt { get; }

    [JsonIgnore]
    public string AggregateType => "Incident";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
