using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A product node moved to a different lifecycle status.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="ProductLifecycleChangedEventV2"/> replaced it.
/// Kept so every payload written as this type still deserializes into it — its name and members are the
/// contract those payloads were written against, so neither may change.
/// </remarks>
[Obsolete("Superseded by ProductLifecycleChangedEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record ProductLifecycleChangedEvent : DomainEvent, IProductManagementEvent
{
    [JsonConstructor]
    public ProductLifecycleChangedEvent(
        Guid id,
        int key,
        string name,
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
        Name = name;
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
    public string Name { get; }

    public Guid FromStatusId { get; }
    public StatusCategory FromCategory { get; }
    public ProductStatusAlias FromAlias { get; }

    public Guid ToStatusId { get; }
    public StatusCategory ToCategory { get; }
    public ProductStatusAlias ToAlias { get; }

    [JsonIgnore]
    public string AggregateType => "Product";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
