using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Events.ProductManagement.Incidents;

/// <summary>An incident moved to a different status.</summary>
/// <remarks>
/// Response and recovery instants ride on this event when the move records them. A payload written
/// before those instants deserializes them as null: the move did not record a timeline change.
/// </remarks>
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
        Instant? fromStartedAt = null,
        Instant? toStartedAt = null,
        Instant? fromRecoveredAt = null,
        Instant? toRecoveredAt = null)
        : base(actor, "1.1")
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
