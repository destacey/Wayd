using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A tag axis was added to the ones products are labelled along.
/// </summary>
public sealed record ProductTagCategoryCreatedEvent : DomainEvent<ProductTagCategoryCreatedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    public ProductTagCategoryCreatedEvent(Guid id, string name, string? description, bool allowsMany, int order, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Name = name;
        Description = description;
        AllowsMany = allowsMany;
        Order = order;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public string Name { get; }

    /// <summary>What the axis is for, when described.</summary>
    public string? Description { get; }

    /// <summary>Whether a product can carry several tags from the axis.</summary>
    public bool AllowsMany { get; }

    /// <summary>Position when presenting the axes, lowest first.</summary>
    public int Order { get; }

    [JsonIgnore]
    public string AggregateType => "ProductTagCategory";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
