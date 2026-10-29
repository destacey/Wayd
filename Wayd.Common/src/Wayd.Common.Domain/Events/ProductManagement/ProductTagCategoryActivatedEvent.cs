using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A deactivated product tag axis was put back into use.
/// </summary>
public sealed record ProductTagCategoryActivatedEvent : DomainEvent<ProductTagCategoryActivatedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    public ProductTagCategoryActivatedEvent(Guid id, EventActor actor, Instant timestamp)
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
