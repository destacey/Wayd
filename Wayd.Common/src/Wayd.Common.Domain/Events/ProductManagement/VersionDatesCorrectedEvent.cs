using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A version's recorded target, cut or released date was corrected.
/// </summary>
/// <remarks>
/// Distinct from <see cref="VersionCutEvent"/> and <see cref="VersionReleasedEvent"/>, which say the
/// version moved. This says only that what was written down was wrong, so it carries both ends: the
/// value that was replaced is the whole point of recording the correction.
/// <para>
/// Frozen at its published shape and never raised; <see cref="VersionDatesCorrectedEventV2"/> replaced it. Kept so every payload
/// written as this type still deserializes into it, so neither its name nor its members may change.
/// </para>
/// </remarks>
[Obsolete("Superseded by VersionDatesCorrectedEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record VersionDatesCorrectedEvent : DomainEvent<VersionDatesCorrectedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.ScheduleChanged;

    [JsonConstructor]
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
        : base(actor, "1.0")
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

    [JsonIgnore]
    public string AggregateType => "Version";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
