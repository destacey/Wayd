using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A version was pulled after being cut.
/// </summary>
/// <remarks>
/// Its own type because this is phase one's failure proxy: withdrawal rate is the measure the version
/// record supports honestly, without work items or pipeline data. Folding it into a generic status
/// change would push the interesting distinction into a field every consumer then has to inspect.
/// </remarks>
public sealed record VersionWithdrawnEvent : DomainEvent, IProductManagementEvent
{
    public VersionWithdrawnEvent(Guid id, int key, Guid productId, string productName, string number, string? reason, Guid statusId, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        ProductId = productId;
        ProductName = productName;
        Number = number;
        Reason = reason;
        StatusId = statusId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ProductId { get; }
    public string ProductName { get; }
    public string Number { get; }

    /// <summary>Why it was pulled, where someone recorded a reason.</summary>
    public string? Reason { get; }

    public Guid StatusId { get; }
}
