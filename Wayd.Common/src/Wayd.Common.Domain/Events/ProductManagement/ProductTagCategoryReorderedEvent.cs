using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A product tag axis moved to another position in the list.
/// </summary>
/// <remarks>
/// Raised by each axis whose position changed, so reordering the list records one entry per axis moved.
/// </remarks>
public sealed record ProductTagCategoryReorderedEvent : DomainEvent<ProductTagCategoryReorderedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ProductTagCategoryReorderedEvent(Guid id, int fromOrder, int toOrder, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        FromOrder = fromOrder;
        ToOrder = toOrder;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    /// <summary>The axis's position before the move, lowest first.</summary>
    public int FromOrder { get; }

    /// <summary>The axis's position after the move, lowest first.</summary>
    public int ToOrder { get; }

    [JsonIgnore]
    public string AggregateType => "ProductTagCategory";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
