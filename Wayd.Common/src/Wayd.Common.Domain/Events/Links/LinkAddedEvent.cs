using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Links;

/// <summary>
/// A link was added to a record.
/// </summary>
public sealed record LinkAddedEvent : DomainEvent<LinkAddedEvent>, IDomainEventDescriptor, ILinkEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public LinkAddedEvent(Guid id, string ownerType, Guid ownerId, string name, string url, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        OwnerType = ownerType;
        OwnerId = ownerId;
        Name = name;
        Url = url;

        Timestamp = timestamp;
    }

    /// <summary>The link's id.</summary>
    public Guid Id { get; }

    /// <inheritdoc/>
    public string OwnerType { get; }

    /// <inheritdoc/>
    public Guid OwnerId { get; }

    /// <summary>What the link is called.</summary>
    public string Name { get; }

    /// <summary>The address the link opens.</summary>
    public string Url { get; }

    [JsonIgnore]
    public string AggregateType => "Link";

    [JsonIgnore]
    public Guid AggregateId => Id;

    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => LinkEventRelations.For(OwnerType, OwnerId);
}
