using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Links;

/// <summary>
/// A link was removed from a record, by hand or because the record itself was deleted.
/// </summary>
public sealed record LinkRemovedEvent : DomainEvent<LinkRemovedEvent>, IDomainEventDescriptor, ILinkEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    [JsonConstructor]
    public LinkRemovedEvent(Guid id, string ownerType, Guid ownerId, string name, string url, EventActor actor, Instant timestamp)
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

    /// <summary>What the link was called.</summary>
    public string Name { get; }

    /// <summary>The address the link opened.</summary>
    public string Url { get; }

    [JsonIgnore]
    public string AggregateType => "Link";

    [JsonIgnore]
    public Guid AggregateId => Id;

    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => LinkEventRelations.For(OwnerType, OwnerId);
}
