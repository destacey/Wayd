using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// The description of a product's dependency changed.
/// </summary>
public sealed record ProductDependencyDetailsUpdatedEvent : DomainEvent<ProductDependencyDetailsUpdatedEvent>, IDomainEventDescriptor, IProductManagementEvent, IRelatedAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ProductDependencyDetailsUpdatedEvent(Guid id, int key, Guid dependencyId, Guid dependsOnProductId, string? description, string? previousDescription, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        DependencyId = dependencyId;
        DependsOnProductId = dependsOnProductId;
        Description = description;
        PreviousDescription = previousDescription;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid DependencyId { get; }
    public Guid DependsOnProductId { get; }
    public string? Description { get; }

    /// <summary>The description this edit replaced, or <c>null</c> where there was none.</summary>
    public string? PreviousDescription { get; }

    [JsonIgnore]
    public string AggregateType => "Product";
    [JsonIgnore]
    public Guid AggregateId => Id;

    /// <summary>The product depended on, whose Activity shows the dependency on it being reworded.</summary>
    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => [new(AggregateType, DependsOnProductId)];
}
