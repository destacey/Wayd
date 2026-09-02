using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A version recorded as shipped did not in fact ship, and was moved back.
/// </summary>
/// <remarks>
/// Deliberately not a <see cref="VersionWithdrawnEvent"/>. A withdrawal says a real shipment was
/// pulled; this says the shipment never happened and the record was wrong. A consumer counting
/// shipments must subtract this one rather than treat it as a shipment that was later reversed.
/// <para>
/// Carries the released date that was cleared, because the correction is only legible against the
/// value it replaced.
/// </para>
/// </remarks>
public sealed record VersionRevertedEvent : DomainEvent, IProductManagementEvent
{
    public VersionRevertedEvent(
        Guid id,
        int key,
        Guid productId,
        string productName,
        string number,
        LocalDate fromReleasedDate,
        string reason,
        Guid statusId,
        EventActor actor,
        Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        ProductId = productId;
        ProductName = productName;
        Number = number;
        FromReleasedDate = fromReleasedDate;
        Reason = reason;
        StatusId = statusId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ProductId { get; }
    public string ProductName { get; }
    public string Number { get; }

    /// <summary>The released date that was cleared by the revert.</summary>
    public LocalDate FromReleasedDate { get; }

    /// <summary>Why the version was reverted. Required — this contradicts what the history asserts.</summary>
    public string Reason { get; }

    public Guid StatusId { get; }
}
