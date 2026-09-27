using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A version was cut — its scope is fixed and it is ready to ship.
/// </summary>
/// <remarks>
/// Distinct from <see cref="VersionReleasedEvent"/> because cut-to-released is the latency measure
/// phase one reports, and it needs both ends as separate facts.
/// <para>
/// Frozen at its published shape and never raised; <see cref="VersionCutEventV2"/> replaced it. Kept so every payload
/// written as this type still deserializes into it, so neither its name nor its members may change.
/// </para>
/// </remarks>
[Obsolete("Superseded by VersionCutEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record VersionCutEvent : DomainEvent<VersionCutEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StatusChanged;

    [JsonConstructor]
    public VersionCutEvent(Guid id, int key, Guid productId, string productName, string number, LocalDate cutDate, Guid statusId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        ProductId = productId;
        ProductName = productName;
        Number = number;
        CutDate = cutDate;
        StatusId = statusId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ProductId { get; }
    public string ProductName { get; }
    public string Number { get; }
    public LocalDate CutDate { get; }
    public Guid StatusId { get; }

    [JsonIgnore]
    public string AggregateType => "Version";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
