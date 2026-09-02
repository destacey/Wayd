using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A version shipped.
/// </summary>
/// <remarks>
/// The event release frequency counts. Note that durable dispatch gives no cross-handler ordering
/// guarantee, so this can arrive before the <see cref="VersionCutEvent"/> for the same version — a
/// consumer that renders a timeline should order on the dates carried here rather than on arrival.
/// </remarks>
public sealed record VersionReleasedEvent : DomainEvent, IProductManagementEvent
{
    public VersionReleasedEvent(Guid id, int key, Guid productId, string productName, string number, LocalDate releasedDate, Guid statusId, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        ProductId = productId;
        ProductName = productName;
        Number = number;
        ReleasedDate = releasedDate;
        StatusId = statusId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ProductId { get; }
    public string ProductName { get; }
    public string Number { get; }
    public LocalDate ReleasedDate { get; }
    public Guid StatusId { get; }
}
