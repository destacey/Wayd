using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A release's version label, name, notes, owning product or ordering sequence changed.
/// </summary>
/// <remarks>
/// Shared across those fields because handling is identical everywhere.
/// </remarks>
public sealed record ReleaseDetailsUpdatedEvent : DomainEvent, IProductManagementEvent
{
    public ReleaseDetailsUpdatedEvent(Guid id, int key, Guid? productId, string version, string? name, long? sequence, EventActor actor, Instant timestamp)
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

    /// <summary>The product node the release is announced under, where it is scoped to one.</summary>
    public Guid? ProductId { get; }

    public string Version { get; }
    public string? Name { get; }

    /// <summary>The manual ordering override, or <c>null</c> when the release orders by chronology.</summary>
    public long? Sequence { get; }
}
