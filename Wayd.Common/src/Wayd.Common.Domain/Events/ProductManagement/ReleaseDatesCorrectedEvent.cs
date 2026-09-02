using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A release's recorded target or released date was corrected.
/// </summary>
/// <remarks>
/// Distinct from <see cref="ReleaseReleasedEvent"/>, which says the release was announced. This says
/// only that what was written down was wrong, so it carries both ends: the value that was replaced is
/// the whole point of recording the correction.
/// <para>
/// No cut date, unlike <see cref="VersionDatesCorrectedEvent"/> — a release is never cut.
/// </para>
/// </remarks>
public sealed record ReleaseDatesCorrectedEvent : DomainEvent, IProductManagementEvent
{
    public ReleaseDatesCorrectedEvent(
        Guid id,
        int key,
        Guid? productId,
        string version,
        LocalDate? fromTargetDate,
        LocalDate? toTargetDate,
        LocalDate? fromReleasedDate,
        LocalDate? toReleasedDate,
        EventActor actor,
        Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        ProductId = productId;
        Version = version;
        FromTargetDate = fromTargetDate;
        ToTargetDate = toTargetDate;
        FromReleasedDate = fromReleasedDate;
        ToReleasedDate = toReleasedDate;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid? ProductId { get; }
    public string Version { get; }
    public LocalDate? FromTargetDate { get; }
    public LocalDate? ToTargetDate { get; }
    public LocalDate? FromReleasedDate { get; }
    public LocalDate? ToReleasedDate { get; }
}
