using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A product node was removed from the taxonomy.
/// </summary>
public sealed record ProductRemovedEvent : DomainEvent, IProductManagementEvent
{
    public ProductRemovedEvent(Guid id, int key, string name, Guid? parentId, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        Name = name;
        ParentId = parentId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }

    /// <summary>The parent it hung from, so a consumer can invalidate that subtree's rollups.</summary>
    public Guid? ParentId { get; }
}
