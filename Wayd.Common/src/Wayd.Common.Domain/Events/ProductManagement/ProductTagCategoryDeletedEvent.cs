using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A product tag axis no product was tagged along was deleted, with its tags.
/// </summary>
public sealed record ProductTagCategoryDeletedEvent : DomainEvent<ProductTagCategoryDeletedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    public ProductTagCategoryDeletedEvent(Guid id, string name, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Name = name;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "ProductTagCategory";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
