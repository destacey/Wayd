using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A version was deleted, with its status history and every deployment of it.
/// </summary>
/// <remarks>
/// Each deployment removed with it raises its own <see cref="DeploymentDeletedEvent"/>, so this carries
/// no list of them. The product travels so a consumer holding a product's versions can invalidate that
/// product without having kept a copy.
/// </remarks>
public sealed record VersionDeletedEvent : DomainEvent<VersionDeletedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    public VersionDeletedEvent(Guid id, int key, Guid productId, string number, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        ProductId = productId;
        Number = number;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ProductId { get; }
    public string Number { get; }

    [JsonIgnore]
    public string AggregateType => "Version";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
