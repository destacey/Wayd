using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Models;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A product stopped depending on another.
/// </summary>
/// <remarks>
/// The link is kept, and still counts for the period it held. A link that was never true is removed
/// instead — see <see cref="ProductDependencyRemovedEvent"/>.
/// </remarks>
public sealed record ProductDependencyEndedEvent : DomainEvent<ProductDependencyEndedEvent>, IDomainEventDescriptor, IProductManagementEvent, IRelatedAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ProductDependencyEndedEvent(Guid id, int key, Guid dependencyId, Guid dependsOnProductId, DependencyStrength strength, FlexibleDateRange period, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        DependencyId = dependencyId;
        DependsOnProductId = dependsOnProductId;
        Strength = strength;
        Period = period;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid DependencyId { get; }
    public Guid DependsOnProductId { get; }
    public DependencyStrength Strength { get; }
    /// <summary>The days the dependency held, its end being the last of them.</summary>
    public FlexibleDateRange Period { get; }

    [JsonIgnore]
    public string AggregateType => "Product";
    [JsonIgnore]
    public Guid AggregateId => Id;

    /// <summary>The product depended on, whose Activity shows it losing a consumer.</summary>
    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => [new(AggregateType, DependsOnProductId)];
}
