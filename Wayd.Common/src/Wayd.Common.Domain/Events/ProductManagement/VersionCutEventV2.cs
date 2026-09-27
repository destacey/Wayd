using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A version was cut — its scope is fixed and it is ready to ship.
/// </summary>
/// <remarks>
/// Distinct from <see cref="VersionReleasedEventV2"/> because cut-to-released is the latency measure
/// phase one reports, and it needs both ends as separate facts.
/// <para>
/// Supersedes <see cref="VersionCutEvent"/>, whose <c>CutDate</c> held only a calendar date. A new
/// type rather than a new version, because retyping a member breaks every consumer written against the
/// old shape.
/// </para>
/// </remarks>
public sealed record VersionCutEventV2 : DomainEvent<VersionCutEventV2>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StatusChanged;

    [JsonConstructor]
    public VersionCutEventV2(Guid id, int key, Guid productId, string productName, string number, Instant cutAt, Guid statusId, EventActor actor, Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        ProductId = productId;
        ProductName = productName;
        Number = number;
        CutAt = cutAt;
        StatusId = statusId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ProductId { get; }
    public string ProductName { get; }
    public string Number { get; }
    public Instant CutAt { get; }
    public Guid StatusId { get; }

    [JsonIgnore]
    public string AggregateType => "Version";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
