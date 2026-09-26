using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A version shipped.
/// </summary>
/// <remarks>
/// The event release frequency counts. Note that durable dispatch gives no cross-handler ordering
/// guarantee, so this can arrive before the <see cref="VersionCutEventV2"/> for the same version — a
/// consumer that renders a timeline should order on the moments carried here rather than on arrival.
/// <para>
/// Supersedes <see cref="VersionReleasedEvent"/>, whose <c>ReleasedDate</c> held only a calendar date. A new
/// type rather than a new version, because retyping a member breaks every consumer written against the
/// old shape.
/// </para>
/// </remarks>
public sealed record VersionReleasedEventV2 : DomainEvent<VersionReleasedEventV2>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StatusChanged;

    [JsonConstructor]
    public VersionReleasedEventV2(Guid id, int key, Guid productId, string productName, string number, Instant releasedAt, Guid statusId, EventActor actor, Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        ProductId = productId;
        ProductName = productName;
        Number = number;
        ReleasedAt = releasedAt;
        StatusId = statusId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ProductId { get; }
    public string ProductName { get; }
    public string Number { get; }
    public Instant ReleasedAt { get; }
    public Guid StatusId { get; }

    [JsonIgnore]
    public string AggregateType => "Version";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
