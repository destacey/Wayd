using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// The tags on a product node changed.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="ProductTagsChangedEventV2"/> replaced it. Kept
/// so every payload written as this type still deserializes into it — its name and members are the contract
/// those payloads were written against, so neither may change.
/// </remarks>
[Obsolete("Superseded by ProductTagsChangedEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record ProductTagsChangedEvent : DomainEvent, IProductManagementEvent
{
    [JsonConstructor]
    public ProductTagsChangedEvent(Guid id, int key, string name, Guid[] tagIds, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        TagIds = [.. tagIds];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }

    /// <summary>Every tag the product now carries.</summary>
    public Guid[] TagIds { get; }

    [JsonIgnore]
    public string AggregateType => "Product";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
