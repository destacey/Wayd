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
        Instant timestamp)
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

    [JsonIgnore]
    public string AggregateType => "Incident";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
