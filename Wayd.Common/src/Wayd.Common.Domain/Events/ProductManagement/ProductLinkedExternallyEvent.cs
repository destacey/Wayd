using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Interfaces.ProductManagement;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A product node was pointed at the record that owns it in another system, or unlinked.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="ProductLinkedExternallyEventV2"/> replaced it.
/// Kept so every payload written as this type still deserializes into it — its name and members are the
/// contract those payloads were written against, so neither may change.
/// </remarks>
[Obsolete("Superseded by ProductLinkedExternallyEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record ProductLinkedExternallyEvent : DomainEvent, IProductManagementEvent, ISimpleProduct
{
    [JsonConstructor]
    public ProductLinkedExternallyEvent(Guid id, int key, string name, string? description, string? externalId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        ExternalId = externalId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string? Description { get; }
    public string? ExternalId { get; }

    [JsonIgnore]
    public string AggregateType => "Product";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
