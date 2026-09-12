using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A product node was moved to a different parent, or to the root.
/// </summary>
/// <remarks>
/// Its own type rather than part of a details change, because reparenting invalidates every rollup that
/// walks the tree — release scope, ownership inheritance, anything grouped by ancestor. A consumer that
/// ignores a rename cannot ignore this. Carries both ends: where a node moved from is as much of the
/// story as where it landed.
/// <para>
/// Supersedes <see cref="ProductReparentedEvent"/>, dropping its required <c>Name</c>, which described the
/// product rather than the change. A new type rather than a new version, because removing a required member
/// breaks every consumer written against the old shape.
/// </para>
/// </remarks>
public sealed record ProductReparentedEventV2 : DomainEvent, IProductManagementEvent
{
    [JsonConstructor]
    public ProductReparentedEventV2(Guid id, int key, Guid? fromParentId, Guid? toParentId, EventActor actor, Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        FromParentId = fromParentId;
        ToParentId = toParentId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The parent it moved from, or <c>null</c> when it was a root node.</summary>
    public Guid? FromParentId { get; }

    /// <summary>The parent it moved to, or <c>null</c> when it became a root node.</summary>
    public Guid? ToParentId { get; }

    [JsonIgnore]
    public string AggregateType => "Product";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
