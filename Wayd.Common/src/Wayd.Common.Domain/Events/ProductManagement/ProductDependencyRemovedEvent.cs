using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Models;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A dependency recorded on a product was deleted as never having been true.
/// </summary>
/// <remarks>
/// Not a dependency stopping — that is <see cref="ProductDependencyEndedEvent"/>, which keeps the link. This
/// says the record was wrong, so it carries the whole link as it stood, and the reason, because it
/// contradicts something the history already asserted.
/// </remarks>
public sealed record ProductDependencyRemovedEvent : DomainEvent<ProductDependencyRemovedEvent>, IDomainEventDescriptor, IProductManagementEvent, IRelatedAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ProductDependencyRemovedEvent(Guid id, int key, Guid dependencyId, Guid dependsOnProductId, DependencyStrength strength, IReadOnlyCollection<InteractionStyle>? interactionStyles, FlexibleDateRange period, string reason, EventActor actor, Instant timestamp)
        : base(actor, "1.1")
    {
        Id = id;
        Key = key;
        DependencyId = dependencyId;
        DependsOnProductId = dependsOnProductId;
        Strength = strength;
        InteractionStyles = interactionStyles;
        Period = period;
        Reason = reason;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid DependencyId { get; }
    public Guid DependsOnProductId { get; }
    public DependencyStrength Strength { get; }

    /// <summary>
    /// How the product had been recorded as reaching the one it depended on, or <c>null</c> where nobody
    /// recorded it.
    /// </summary>
    /// <remarks>Added at 1.1. Null on every payload written before it.</remarks>
    public IReadOnlyCollection<InteractionStyle>? InteractionStyles { get; }

    /// <summary>The days the removed link covered, with no end where it was still open.</summary>
    public FlexibleDateRange Period { get; }

    public string Reason { get; }

    [JsonIgnore]
    public string AggregateType => "Product";
    [JsonIgnore]
    public Guid AggregateId => Id;

    /// <summary>The product that had been recorded as depended on.</summary>
    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => [new(AggregateType, DependsOnProductId)];
}
