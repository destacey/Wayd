using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A version shipped.
/// </summary>
/// <remarks>
/// The event release frequency counts. Note that durable dispatch gives no cross-handler ordering
/// guarantee, so this can arrive before the <see cref="VersionCutEvent"/> for the same version — a
/// consumer that renders a timeline should order on the dates carried here rather than on arrival.
/// <para>
/// Frozen at its published shape and never raised; <see cref="VersionReleasedEventV2"/> replaced it. Kept so every payload
/// written as this type still deserializes into it, so neither its name nor its members may change.
/// </para>
/// </remarks>
[Obsolete("Superseded by VersionReleasedEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record VersionReleasedEvent : DomainEvent<VersionReleasedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StatusChanged;

    [JsonConstructor]
    public VersionReleasedEvent(Guid id, int key, Guid productId, string productName, string number, LocalDate releasedDate, Guid statusId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        ProductId = productId;
        ProductName = productName;
        Number = number;
        ReleasedDate = releasedDate;
        StatusId = statusId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ProductId { get; }
    public string ProductName { get; }
    public string Number { get; }
    public LocalDate ReleasedDate { get; }
    public Guid StatusId { get; }

    [JsonIgnore]
    public string AggregateType => "Version";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
