using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Models;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A team or team of teams was linked to a product — Payments on Storefront Web.
/// </summary>
/// <remarks>
/// Filed under the product alone. Teams and products are read under different permissions, so naming the team
/// as a related record would show this on the team's Activity to readers not allowed to see products.
/// </remarks>
public sealed record ProductTeamLinkedEvent : DomainEvent<ProductTeamLinkedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ProductTeamLinkedEvent(Guid id, int key, Guid teamId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        TeamId = teamId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The team or team of teams linked.</summary>
    public Guid TeamId { get; }

    [JsonIgnore]
    public string AggregateType => "Product";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
