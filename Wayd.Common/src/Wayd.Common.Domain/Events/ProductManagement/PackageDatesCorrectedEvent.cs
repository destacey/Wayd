using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A release package's recorded target or released date was corrected.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="PackageDatesCorrectedEventV2"/> replaced it.
/// Kept so every payload written as this type still deserializes into it — its name and members are the
/// contract those payloads were written against, so neither may change.
/// </remarks>
[Obsolete("Superseded by PackageDatesCorrectedEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record PackageDatesCorrectedEvent : DomainEvent<PackageDatesCorrectedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.ScheduleChanged;

    [JsonConstructor]
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
