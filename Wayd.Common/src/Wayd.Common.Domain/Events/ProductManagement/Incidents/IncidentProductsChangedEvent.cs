using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement.Incidents;

/// <summary>The products an incident implicates were replaced.</summary>
public sealed record IncidentProductsChangedEvent : DomainEvent<IncidentProductsChangedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public IncidentProductsChangedEvent(
        Guid id,
        int key,
        IReadOnlyCollection<Guid> addedProductIds,
        IReadOnlyCollection<Guid> removedProductIds,
        IReadOnlyCollection<Guid> productIds,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        AddedProductIds = addedProductIds;
        RemovedProductIds = removedProductIds;
        ProductIds = productIds;

        Timestamp = timestamp;
    }

    /// <summary>The incident.</summary>
    public Guid Id { get; }

    /// <summary>The incident's key.</summary>
    public int Key { get; }

    /// <summary>Products added by this change.</summary>
    public IReadOnlyCollection<Guid> AddedProductIds { get; }

    /// <summary>Products removed by this change.</summary>
    public IReadOnlyCollection<Guid> RemovedProductIds { get; }

    /// <summary>The products the incident implicates after the change.</summary>
    public IReadOnlyCollection<Guid> ProductIds { get; }

    [JsonIgnore]
    public string AggregateType => "Incident";

    [JsonIgnore]
    public Guid AggregateId => Id;
}
