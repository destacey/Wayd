using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A version's recorded target, cut or released date was corrected.
/// </summary>
/// <remarks>
/// Distinct from <see cref="VersionCutEvent"/> and <see cref="VersionReleasedEvent"/>, which say the
/// version moved. This says only that what was written down was wrong, so it carries both ends: the
/// value that was replaced is the whole point of recording the correction.
/// </remarks>
public sealed record VersionDatesCorrectedEvent : DomainEvent, IProductManagementEvent
{
    public VersionDatesCorrectedEvent(
        Guid id,
        int key,
        Guid productId,
        string productName,
        string number,
        LocalDate? fromTargetDate,
        LocalDate? toTargetDate,
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
        Number = number;
        FromTargetDate = fromTargetDate;
        ToTargetDate = toTargetDate;
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
    public string Number { get; }
    public LocalDate? FromTargetDate { get; }
    public LocalDate? ToTargetDate { get; }
    public LocalDate? FromCutDate { get; }
    public LocalDate? ToCutDate { get; }
    public LocalDate? FromReleasedDate { get; }
    public LocalDate? ToReleasedDate { get; }
}
