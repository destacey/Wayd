using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A version recorded as shipped did not in fact ship, and was moved back.
/// </summary>
/// <remarks>
/// Deliberately not a <see cref="VersionWithdrawnEvent"/>. A withdrawal says a real shipment was
/// pulled; this says the shipment never happened and the record was wrong. A consumer counting
/// shipments must subtract this one rather than treat it as a shipment that was later reversed.
/// <para>
/// Carries the released moment that was cleared, because the correction is only legible against the
/// value it replaced.
/// </para>
/// <para>
/// Supersedes <see cref="VersionRevertedEvent"/>, whose <c>FromReleasedDate</c> held only a calendar date. A new
/// type rather than a new version, because retyping a member breaks every consumer written against the
/// old shape.
/// </para>
/// </remarks>
public sealed record VersionRevertedEventV2 : DomainEvent<VersionRevertedEventV2>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StatusChanged;

    [JsonConstructor]
    public VersionRevertedEventV2(
        Guid id,
        int key,
        Guid productId,
        string productName,
        string number,
        Instant fromReleasedAt,
        string reason,
        Guid statusId,
        EventActor actor,
        Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        ProductId = productId;
        ProductName = productName;
        Number = number;
        FromReleasedAt = fromReleasedAt;
        Reason = reason;
        StatusId = statusId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ProductId { get; }
    public string ProductName { get; }
    public string Number { get; }

    /// <summary>The released moment that was cleared by the revert.</summary>
    public Instant FromReleasedAt { get; }

    /// <summary>Why the version was reverted. Required — this contradicts what the history asserts.</summary>
    public string Reason { get; }

    public Guid StatusId { get; }

    [JsonIgnore]
    public string AggregateType => "Version";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
