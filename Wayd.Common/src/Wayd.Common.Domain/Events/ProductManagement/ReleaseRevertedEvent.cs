using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A release recorded as announced was not in fact announced, and was moved back.
/// </summary>
/// <remarks>
/// Deliberately not a <see cref="ReleaseWithdrawnEvent"/>. A retraction says a real announcement was
/// pulled; this says the announcement never happened and the record was wrong.
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
        Guid? productId,
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
        Version = version;
        FromReleasedDate = fromReleasedDate;
        Reason = reason;
        StatusId = statusId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid? ProductId { get; }
    public string Version { get; }

    /// <summary>The released date that was cleared by the revert.</summary>
    public LocalDate FromReleasedDate { get; }

    /// <summary>Why the release was reverted. Required — this contradicts what the history asserts.</summary>
    public string Reason { get; }

    public Guid StatusId { get; }
}
