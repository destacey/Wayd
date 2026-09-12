using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A product node's type changed.
/// </summary>
/// <remarks>
/// Separate from a details change because the type carries capability flags: a node that stops being
/// releasable invalidates assumptions a consumer may have cached about whether releases can be cut
/// against it.
/// <para>
/// Supersedes <see cref="ProductRetypedEvent"/>, dropping its required <c>Name</c>, which described the
/// product rather than the change. A new type rather than a new version, because removing a required member
/// breaks every consumer written against the old shape.
/// </para>
/// </remarks>
public sealed record ProductRetypedEventV2 : DomainEvent, IProductManagementEvent
{
    [JsonConstructor]
    public ProductRetypedEventV2(Guid id, int key, Guid fromProductTypeId, Guid toProductTypeId, EventActor actor, Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        FromProductTypeId = fromProductTypeId;
        ToProductTypeId = toProductTypeId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid FromProductTypeId { get; }
    public Guid ToProductTypeId { get; }

    [JsonIgnore]
    public string AggregateType => "Product";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
