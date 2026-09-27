using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A version's recorded target date, cut moment or released moment was corrected.
/// </summary>
/// <remarks>
/// Distinct from <see cref="VersionCutEventV2"/> and <see cref="VersionReleasedEventV2"/>, which say the
/// version moved. This says only that what was written down was wrong, so it carries both ends: the
/// value that was replaced is the whole point of recording the correction.
/// <para>
/// Supersedes <see cref="VersionDatesCorrectedEvent"/>, whose cut and released ends were calendar dates.
/// A new type rather than a new version, because retyping a member breaks every consumer written against
/// the old shape.
/// </para>
/// </remarks>
public sealed record VersionDatesCorrectedEventV2 : DomainEvent<VersionDatesCorrectedEventV2>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.ScheduleChanged;

    [JsonConstructor]
    public VersionDatesCorrectedEventV2(
        Guid id,
        int key,
        Guid productId,
        string productName,
        string number,
        LocalDate? fromTargetDate,
        LocalDate? toTargetDate,
        Instant? fromCutAt,
        Instant? toCutAt,
        Instant? fromReleasedAt,
        Instant? toReleasedAt,
        EventActor actor,
        Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        ProductId = productId;
        ProductName = productName;
        Number = number;
        FromTargetDate = fromTargetDate;
        ToTargetDate = toTargetDate;
        FromCutAt = fromCutAt;
        ToCutAt = toCutAt;
        FromReleasedAt = fromReleasedAt;
        ToReleasedAt = toReleasedAt;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ProductId { get; }
    public string ProductName { get; }
    public string Number { get; }
    public LocalDate? FromTargetDate { get; }
    public LocalDate? ToTargetDate { get; }
    public Instant? FromCutAt { get; }
    public Instant? ToCutAt { get; }
    public Instant? FromReleasedAt { get; }
    public Instant? ToReleasedAt { get; }

    [JsonIgnore]
    public string AggregateType => "Version";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
