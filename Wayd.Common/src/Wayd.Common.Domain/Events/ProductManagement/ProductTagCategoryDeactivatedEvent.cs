using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A product tag axis was taken out of use: nothing new is tagged along it, and products already tagged
/// keep their tags.
/// </summary>
public sealed record ProductTagCategoryDeactivatedEvent : DomainEvent<ProductTagCategoryDeactivatedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    public ProductTagCategoryDeactivatedEvent(Guid id, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    [JsonIgnore]
    public string AggregateType => "ProductTagCategory";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
