using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.ProductManagement.Application.DeliveryOverview.Dtos;

/// <summary>What kind of record an event happened to.</summary>
public enum DeliveryRecordKind
{
    Version = 1,
    ReleasePackage = 2,
}

/// <summary>
/// One thing that happened to a version or a package, most recent first.
/// </summary>
/// <remarks>
/// Read from the status transitions rather than from the records' own dates, for two reasons. The
/// dates are <c>LocalDate</c> — they carry no time of day, so a feed built from them could not order
/// two things that happened on one afternoon. And a date says the state a record is in now, while a
/// transition says the moment it changed, which is what a feed is a list of.
/// </remarks>
public sealed record RecentDeliveryEventDto
{
    public Guid RecordId { get; init; }
    public int RecordKey { get; init; }

    /// <summary>Which record this happened to, so a caller knows where the link goes.</summary>
    public DeliveryRecordKind Kind { get; init; }

    /// <summary>The product the version was cut against. Null for a package, which spans several.</summary>
    public NavigationDto? Product { get; init; }

    /// <summary>The version number, or the package's own version. Free text, never parsed.</summary>
    public string Label { get; init; } = default!;

    /// <summary>The status reached, as the organization named it.</summary>
    public string StatusName { get; init; } = default!;

    /// <summary>
    /// The well-known meaning of that status.
    /// </summary>
    /// <remarks>
    /// The feed styles on this rather than the name, so an organization that renames "Released" to
    /// "Shipped" still gets the same treatment.
    /// </remarks>
    public ProductStatusAlias Alias { get; init; }

    /// <summary>When it changed.</summary>
    public Instant ChangedOn { get; init; }

    /// <summary>
    /// When the record shipped, where it has shipped.
    /// </summary>
    /// <remarks>
    /// Carried so a withdrawal can say what it withdrew — the event alone would leave a reader
    /// asking when the thing being pulled had gone out.
    /// </remarks>
    public LocalDate? ReleasedDate { get; init; }

    /// <summary>How many components the package shipped. Null for a version.</summary>
    public int? ComponentCount { get; init; }
}
