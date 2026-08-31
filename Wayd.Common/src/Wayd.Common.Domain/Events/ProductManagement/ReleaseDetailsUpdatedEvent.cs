using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A release's version, name, notes or ordering sequence changed.
/// </summary>
/// <remarks>
/// Shared across those fields because handling is identical everywhere. The version is included here
/// rather than in an event of its own: Wayd observes releases rather than owning them, so editing a
/// version is an ordinary change, not a lifecycle event.
/// </remarks>
public sealed record ReleaseDetailsUpdatedEvent : DomainEvent, IProductManagementEvent
{
    public ReleaseDetailsUpdatedEvent(Guid id, int key, Guid productId, string version, string? name, long? sequence, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        ProductId = productId;
        Version = version;
        Name = name;
        Sequence = sequence;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ProductId { get; }
    public string Version { get; }
    public string? Name { get; }

    /// <summary>The manual ordering override, or <c>null</c> when the release orders by chronology.</summary>
    public long? Sequence { get; }
}
