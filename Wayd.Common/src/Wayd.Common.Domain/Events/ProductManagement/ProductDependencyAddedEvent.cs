using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Models;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A product began depending on another.
/// </summary>
/// <remarks>
/// Also raised when a dependency's strength changes, after the <see cref="ProductDependencyEndedEvent"/> for
/// the link it replaces: strength is fixed for a link's life, so a change is one link ending and another
/// starting, and both are facts.
/// </remarks>
public sealed record ProductDependencyAddedEvent : DomainEvent<ProductDependencyAddedEvent>, IDomainEventDescriptor, IProductManagementEvent, IRelatedAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ProductDependencyAddedEvent(Guid id, int key, Guid dependencyId, Guid dependsOnProductId, DependencyStrength strength, IReadOnlyCollection<InteractionStyle>? interactionStyles, string? description, FlexibleDateRange period, EventActor actor, Instant timestamp)
        : base(actor, "1.1")
    {
        Id = id;
        Key = key;
        DependencyId = dependencyId;
        DependsOnProductId = dependsOnProductId;
        Strength = strength;
        InteractionStyles = interactionStyles;
        Description = description;
        Period = period;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid DependencyId { get; }
    public Guid DependsOnProductId { get; }
    public DependencyStrength Strength { get; }

    /// <summary>
    /// How the product reaches the one it depends on, or <c>null</c> where nobody has recorded it.
    /// </summary>
    /// <remarks>
    /// Added at 1.1. Null on every payload written before it, which is also what a link with no styles
    /// recorded carries — the two are the same thing, so nothing has to tell them apart.
    /// </remarks>
    public IReadOnlyCollection<InteractionStyle>? InteractionStyles { get; }

    public string? Description { get; }
    /// <summary>The days the dependency holds, from the day it began. Open-ended, since it has just begun.</summary>
    public FlexibleDateRange Period { get; }

    [JsonIgnore]
    public string AggregateType => "Product";
    [JsonIgnore]
    public Guid AggregateId => Id;

    /// <summary>The product depended on, whose Activity shows it gaining a consumer.</summary>
    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => [new(AggregateType, DependsOnProductId)];
}
