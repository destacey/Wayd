using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A tag on a product tag axis was renamed or its description changed. Every product carrying it reads
/// the new name.
/// </summary>
public sealed record ProductTagDetailsUpdatedEvent : DomainEvent<ProductTagDetailsUpdatedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ProductTagDetailsUpdatedEvent(Guid id, Guid tagId, string name, string? description, string previousName, string? previousDescription, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        TagId = tagId;
        Name = name;
        Description = description;
        PreviousName = previousName;
        PreviousDescription = previousDescription;

        Timestamp = timestamp;
    }

    /// <summary>The tag's axis.</summary>
    public Guid Id { get; }
    public Guid TagId { get; }
    public string Name { get; }
    public string? Description { get; }
    public string PreviousName { get; }
    public string? PreviousDescription { get; }

    [JsonIgnore]
    public string AggregateType => "ProductTagCategory";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
