using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Links;

/// <summary>
/// Tracking began for a link that existed before <see cref="LinkAddedEvent"/> was recorded. Carries that event's
/// payload, describing the link as it stood at <see cref="DomainEvent.Timestamp"/>. Also listed on the owning
/// record's Activity, as the creation event is.
/// </summary>
public sealed record LinkBaselinedEvent : BaselineEvent<LinkBaselinedEvent, LinkAddedEvent>, IRelatedAggregateEvent
{
    public LinkBaselinedEvent(
        Guid id,
        string ownerType,
        Guid ownerId,
        string name,
        string url,
        Instant? recordCreatedOn,
        Guid? recordCreatedById,
        Instant timestamp)
        : base("Link", id, recordCreatedOn, recordCreatedById, timestamp, "1.0")
    {
        Id = id;
        OwnerType = ownerType;
        OwnerId = ownerId;
        Name = name;
        Url = url;
    }

    public Guid Id { get; }
    public string OwnerType { get; }
    public Guid OwnerId { get; }
    public string Name { get; }
    public string Url { get; }

    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => LinkEventRelations.For(OwnerType, OwnerId);
}
