using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A tag on a product tag axis was taken out of use. Products already carrying it keep it.
/// </summary>
public sealed record ProductTagDeactivatedEvent : DomainEvent<ProductTagDeactivatedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    public ProductTagDeactivatedEvent(Guid id, Guid tagId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        TagId = tagId;

        Timestamp = timestamp;
    }

    /// <summary>The tag's axis.</summary>
    public Guid Id { get; }
    public Guid TagId { get; }

    [JsonIgnore]
    public string AggregateType => "ProductTagCategory";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
