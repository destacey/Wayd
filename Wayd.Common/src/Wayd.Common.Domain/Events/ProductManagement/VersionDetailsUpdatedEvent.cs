using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A version's number, name, notes or ordering sequence changed.
/// </summary>
/// <remarks>
/// Shared across those fields because handling is identical everywhere. The version is included here
/// rather than in an event of its own: Wayd observes versions rather than owning them, so editing a
/// version is an ordinary change, not a lifecycle event.
/// </remarks>
public sealed record VersionDetailsUpdatedEvent : DomainEvent, IProductManagementEvent
{
    public VersionDetailsUpdatedEvent(Guid id, int key, Guid productId, string number, string? name, long? sequence, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        ProductId = productId;
        Number = number;
        Name = name;
        Sequence = sequence;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ProductId { get; }
    public string Number { get; }
    public string? Name { get; }

    /// <summary>The manual ordering override, or <c>null</c> when the version orders by chronology.</summary>
    public long? Sequence { get; }
}
