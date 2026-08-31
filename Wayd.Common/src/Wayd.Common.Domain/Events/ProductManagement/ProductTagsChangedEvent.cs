using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// The tags on a product node changed.
/// </summary>
/// <remarks>
/// Carries the whole resulting set rather than what was added or removed, because a consumer keeping a
/// projection wants the current state and diffing to discover it is work it should not have to do.
/// One event for both directions: adding and removing a label lead to identical handling.
/// </remarks>
public sealed record ProductTagsChangedEvent : DomainEvent, IProductManagementEvent
{
    public ProductTagsChangedEvent(Guid id, int key, string name, Guid[] tagIds, EventActor actor, Instant timestamp)
        : base(actor)
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
}
