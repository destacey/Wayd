using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A product node's type changed.
/// </summary>
/// <remarks>
/// Separate from a details change because the type carries capability flags: a node that stops being
/// releasable invalidates assumptions a consumer may have cached about whether releases can be cut
/// against it.
/// </remarks>
public sealed record ProductRetypedEvent : DomainEvent, IProductManagementEvent
{
    public ProductRetypedEvent(Guid id, int key, string name, Guid fromProductTypeId, Guid toProductTypeId, EventActor actor, Instant timestamp)
        : base(actor)
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
}
