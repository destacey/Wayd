using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A release's recorded cut or released date was corrected.
/// </summary>
/// <remarks>
/// Distinct from <see cref="ReleaseCutEvent"/> and <see cref="ReleaseReleasedEvent"/>, which say the
/// release moved. This says only that what was written down was wrong, so it carries both ends: the
/// value that was replaced is the whole point of recording the correction.
/// </remarks>
public sealed record ReleaseDatesCorrectedEvent : DomainEvent, IProductManagementEvent
{
    public ReleaseDatesCorrectedEvent(
        Guid id,
        int key,
        Guid productId,
        string productName,
        string version,
        LocalDate? fromCutDate,
        LocalDate? toCutDate,
        LocalDate? fromReleasedDate,
        LocalDate? toReleasedDate,
        EventActor actor,
        Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        ProductId = productId;
        ProductName = productName;
        Version = version;
        FromCutDate = fromCutDate;
        ToCutDate = toCutDate;
        FromReleasedDate = fromReleasedDate;
        ToReleasedDate = toReleasedDate;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ProductId { get; }
    public string ProductName { get; }
    public string Version { get; }
    public LocalDate? FromCutDate { get; }
    public LocalDate? ToCutDate { get; }
    public LocalDate? FromReleasedDate { get; }
    public LocalDate? ToReleasedDate { get; }
}
