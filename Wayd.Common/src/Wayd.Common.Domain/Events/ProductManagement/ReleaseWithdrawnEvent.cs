using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A release was pulled after being cut.
/// </summary>
/// <remarks>
/// Its own type because this is phase one's failure proxy: withdrawal rate is the measure the release
/// record supports honestly, without work items or pipeline data. Folding it into a generic status
/// change would push the interesting distinction into a field every consumer then has to inspect.
/// </remarks>
public sealed record ReleaseWithdrawnEvent : DomainEvent, IProductManagementEvent
{
    public ReleaseWithdrawnEvent(Guid id, int key, Guid productId, string productName, string version, string? reason, Guid statusId, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        ProductId = productId;
        ProductName = productName;
        Version = version;
        Reason = reason;
        StatusId = statusId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ProductId { get; }
    public string ProductName { get; }
    public string Version { get; }

    /// <summary>Why it was pulled, where someone recorded a reason.</summary>
    public string? Reason { get; }

    public Guid StatusId { get; }
}
