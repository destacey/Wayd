using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A release package's recorded target date or released moment was corrected.
/// </summary>
/// <remarks>
/// Distinct from <see cref="PackageReleasedEventV2"/>, which says the package shipped. This says only
/// that what was written down was wrong, so it carries both ends: the value that was replaced is the
/// whole point of recording the correction.
/// <para>
/// Supersedes <see cref="PackageDatesCorrectedEvent"/>, whose released ends were calendar dates. A new type
/// rather than a new version, because retyping a member breaks every consumer written against the old shape.
/// </para>
/// </remarks>
public sealed record PackageDatesCorrectedEventV2 : DomainEvent<PackageDatesCorrectedEventV2>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.ScheduleChanged;

    [JsonConstructor]
    public PackageDatesCorrectedEventV2(
        Guid id,
        int key,
        string version,
        LocalDate? fromTargetDate,
        LocalDate? toTargetDate,
        Instant? fromReleasedAt,
        Instant? toReleasedAt,
        EventActor actor,
        Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        Version = version;
        FromTargetDate = fromTargetDate;
        ToTargetDate = toTargetDate;
        FromReleasedAt = fromReleasedAt;
        ToReleasedAt = toReleasedAt;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Version { get; }
    public LocalDate? FromTargetDate { get; }
    public LocalDate? ToTargetDate { get; }
    public Instant? FromReleasedAt { get; }
    public Instant? ToReleasedAt { get; }

    [JsonIgnore]
    public string AggregateType => "ReleasePackage";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
