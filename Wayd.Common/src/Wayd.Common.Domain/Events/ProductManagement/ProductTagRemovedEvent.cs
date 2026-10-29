using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A tag no product carried was deleted from a product tag axis.
/// </summary>
public sealed record ProductTagRemovedEvent : DomainEvent<ProductTagRemovedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ProductTagRemovedEvent(Guid id, Guid tagId, string name, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        TagId = tagId;
        Name = name;

        Timestamp = timestamp;
    }

    /// <summary>The axis the tag was on.</summary>
    public Guid Id { get; }
    public Guid TagId { get; }

    /// <summary>The tag's name, which nothing else records once it is gone.</summary>
    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "ProductTagCategory";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
