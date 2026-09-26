using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A release package's recorded target or released date was corrected.
/// </summary>
/// <remarks>
/// Distinct from <see cref="PackageReleasedEvent"/>, which says the package shipped. This says only
/// that what was written down was wrong, so it carries both ends: the value that was replaced is the
/// whole point of recording the correction.
/// </remarks>
public sealed record PackageDatesCorrectedEvent : DomainEvent<PackageDatesCorrectedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.ScheduleChanged;

    public PackageDatesCorrectedEvent(
        Guid id,
        int key,
        string version,
        LocalDate? fromTargetDate,
        LocalDate? toTargetDate,
        LocalDate? fromReleasedDate,
        LocalDate? toReleasedDate,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Version = version;
        FromTargetDate = fromTargetDate;
        ToTargetDate = toTargetDate;
        FromReleasedDate = fromReleasedDate;
        ToReleasedDate = toReleasedDate;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Version { get; }
    public LocalDate? FromTargetDate { get; }
    public LocalDate? ToTargetDate { get; }
    public LocalDate? FromReleasedDate { get; }
    public LocalDate? ToReleasedDate { get; }

    [JsonIgnore]
    public string AggregateType => "ReleasePackage";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
