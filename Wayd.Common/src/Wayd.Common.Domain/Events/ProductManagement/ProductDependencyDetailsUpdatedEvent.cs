using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// What a product's dependency says about itself changed — how it is described, or which interaction styles
/// it uses.
/// </summary>
/// <remarks>
/// Not a change of the terms the dependency holds on. Recording styles on a link that had none is a fact
/// somebody finally wrote down, not a fact that changed, so it belongs here rather than starting a new link;
/// changing styles already recorded is a change of terms and does start one.
/// </remarks>
public sealed record ProductDependencyDetailsUpdatedEvent : DomainEvent<ProductDependencyDetailsUpdatedEvent>, IDomainEventDescriptor, IProductManagementEvent, IRelatedAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ProductDependencyDetailsUpdatedEvent(Guid id, int key, Guid dependencyId, Guid dependsOnProductId, string? description, string? previousDescription, IReadOnlyCollection<InteractionStyle>? interactionStyles, IReadOnlyCollection<InteractionStyle>? previousInteractionStyles, EventActor actor, Instant timestamp)
        : base(actor, "1.1")
    {
        Id = id;
        Key = key;
        DependencyId = dependencyId;
        DependsOnProductId = dependsOnProductId;
        Description = description;
        PreviousDescription = previousDescription;
        InteractionStyles = interactionStyles;
        PreviousInteractionStyles = previousInteractionStyles;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid DependencyId { get; }
    public Guid DependsOnProductId { get; }
    public string? Description { get; }

    /// <summary>The description this edit replaced, or <c>null</c> where there was none.</summary>
    public string? PreviousDescription { get; }

    /// <summary>
    /// How the product reaches the one it depends on, after this edit.
    /// </summary>
    /// <remarks>
    /// Added at 1.1, so null on every payload written before it — which is also what an edit that only
    /// reworded the description carries when no styles have ever been recorded. Both ends are given, so a
    /// reader can tell a description edit from a styles edit without holding an earlier payload.
    /// </remarks>
    public IReadOnlyCollection<InteractionStyle>? InteractionStyles { get; }

    /// <summary>The styles this edit replaced, or <c>null</c> where none had been recorded.</summary>
    /// <inheritdoc cref="InteractionStyles" path="/remarks"/>
    public IReadOnlyCollection<InteractionStyle>? PreviousInteractionStyles { get; }

    [JsonIgnore]
    public string AggregateType => "Product";
    [JsonIgnore]
    public Guid AggregateId => Id;

    /// <summary>The product depended on, whose Activity shows the dependency on it being reworded.</summary>
    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => [new(AggregateType, DependsOnProductId)];
}
