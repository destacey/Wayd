using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Models;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A team or team of teams was unlinked from a product.
/// </summary>
/// <remarks>
/// Filed under the product alone, for the reason given on <see cref="ProductTeamLinkedEvent"/>.
/// </remarks>
public sealed record ProductTeamUnlinkedEvent : DomainEvent<ProductTeamUnlinkedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ProductTeamUnlinkedEvent(Guid id, int key, Guid teamId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        TeamId = teamId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The team or team of teams unlinked.</summary>
    public Guid TeamId { get; }

    [JsonIgnore]
    public string AggregateType => "Product";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
