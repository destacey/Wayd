using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A product node's type changed.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="ProductRetypedEventV2"/> replaced it. Kept
/// so every payload written as this type still deserializes into it — its name and members are the contract
/// those payloads were written against, so neither may change.
/// </remarks>
[Obsolete("Superseded by ProductRetypedEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record ProductRetypedEvent : DomainEvent, IProductManagementEvent
{
    [JsonConstructor]
    public ProductRetypedEvent(Guid id, int key, string name, Guid fromProductTypeId, Guid toProductTypeId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        FromProductTypeId = fromProductTypeId;
        ToProductTypeId = toProductTypeId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public Guid FromProductTypeId { get; }
    public Guid ToProductTypeId { get; }

    [JsonIgnore]
    public string AggregateType => "Product";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
