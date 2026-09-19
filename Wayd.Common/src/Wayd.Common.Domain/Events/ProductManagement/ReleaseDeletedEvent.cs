using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A release was deleted, with its contents list and its status history.
/// </summary>
/// <remarks>
/// The versions and packages it listed are separate records and were not touched. The product travels
/// so a consumer holding a product's releases can invalidate that product without having kept a copy.
/// </remarks>
public sealed record ReleaseDeletedEvent : DomainEvent<ReleaseDeletedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    public ReleaseDeletedEvent(Guid id, int key, Guid? productId, string version, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        ProductId = productId;
        Version = version;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid? ProductId { get; }
    public string Version { get; }

    [JsonIgnore]
    public string AggregateType => "Release";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
