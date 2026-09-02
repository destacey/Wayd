using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A release recorded as shipped did not in fact ship, and was moved back.
/// </summary>
/// <remarks>
/// Deliberately not a <see cref="ReleaseWithdrawnEvent"/>. A withdrawal says a real release was
/// pulled; this says the release never happened and the record was wrong. A consumer counting
/// releases must subtract this one rather than treat it as a shipment that was later reversed.
/// <para>
/// Carries the released date that was cleared, because the correction is only legible against the
/// value it replaced.
/// </para>
/// </remarks>
public sealed record ReleaseRevertedEvent : DomainEvent, IProductManagementEvent
{
    public ReleaseRevertedEvent(
        Guid id,
        int key,
        Guid productId,
        string productName,
        string version,
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
        Version = version;
        FromReleasedDate = fromReleasedDate;
        Reason = reason;
        StatusId = statusId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ProductId { get; }
    public string ProductName { get; }
    public string Version { get; }

    /// <summary>The released date that was cleared by the revert.</summary>
    public LocalDate FromReleasedDate { get; }

    /// <summary>Why the release was reverted. Required — this contradicts what the history asserts.</summary>
    public string Reason { get; }

    public Guid StatusId { get; }
}
