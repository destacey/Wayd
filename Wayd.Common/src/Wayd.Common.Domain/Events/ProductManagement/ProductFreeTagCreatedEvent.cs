using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A free product tag — one on no axis — was created by someone typing its name while tagging.
/// </summary>
public sealed record ProductFreeTagCreatedEvent : DomainEvent<ProductFreeTagCreatedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    public ProductFreeTagCreatedEvent(Guid id, string name, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Name = name;

        Timestamp = timestamp;
    }

    /// <summary>The free tag.</summary>
    public Guid Id { get; }

    /// <summary>The tag's name, as first typed.</summary>
    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "ProductTag";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
