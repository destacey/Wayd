using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A product node was moved to a different parent, or to the root.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="ProductReparentedEventV2"/> replaced it. Kept
/// so every payload written as this type still deserializes into it — its name and members are the contract
/// those payloads were written against, so neither may change.
/// </remarks>
[Obsolete("Superseded by ProductReparentedEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record ProductReparentedEvent : DomainEvent, IProductManagementEvent
{
    [JsonConstructor]
    public ProductReparentedEvent(Guid id, int key, string name, Guid? fromParentId, Guid? toParentId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        FromParentId = fromParentId;
        ToParentId = toParentId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    public string Name { get; }

    /// <summary>The parent it moved from, or <c>null</c> when it was a root node.</summary>
    public Guid? FromParentId { get; }

    /// <summary>The parent it moved to, or <c>null</c> when it became a root node.</summary>
    public Guid? ToParentId { get; }

    [JsonIgnore]
    public string AggregateType => "Product";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
