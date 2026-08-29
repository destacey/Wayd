using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A product node was moved to a different parent, or to the root.
/// </summary>
/// <remarks>
/// Its own type rather than part of a details change, because reparenting invalidates every rollup that
/// walks the tree — release scope, ownership inheritance, anything grouped by ancestor. A consumer that
/// ignores a rename cannot ignore this. Carries both ends: where a node moved from is as much of the
/// story as where it landed.
/// </remarks>
public sealed record ProductReparentedEvent : DomainEvent, IProductManagementEvent
{
    public ProductReparentedEvent(Guid id, int key, string name, Guid? fromParentId, Guid? toParentId, EventActor actor, Instant timestamp)
        : base(actor)
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

    /// <summary>
    /// The product's name at the time it moved. Captured so a notification renders without a query, and
    /// stays accurate after a later rename — an event is a historical record.
    /// </summary>
    public string Name { get; }

    /// <summary>The parent it moved from, or <c>null</c> when it was a root node.</summary>
    public Guid? FromParentId { get; }

    /// <summary>The parent it moved to, or <c>null</c> when it became a root node.</summary>
    public Guid? ToParentId { get; }
}
