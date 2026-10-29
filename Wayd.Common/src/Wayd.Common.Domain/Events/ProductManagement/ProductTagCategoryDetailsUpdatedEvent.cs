using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A product tag axis was renamed or its description changed.
/// </summary>
public sealed record ProductTagCategoryDetailsUpdatedEvent : DomainEvent<ProductTagCategoryDetailsUpdatedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ProductTagCategoryDetailsUpdatedEvent(Guid id, string name, string? description, string previousName, string? previousDescription, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Name = name;
        Description = description;
        PreviousName = previousName;
        PreviousDescription = previousDescription;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string? Description { get; }
    public string PreviousName { get; }
    public string? PreviousDescription { get; }

    [JsonIgnore]
    public string AggregateType => "ProductTagCategory";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
