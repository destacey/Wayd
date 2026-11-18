using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Links;

/// <summary>
/// A link's name or address changed.
/// </summary>
public sealed record LinkDetailsUpdatedEvent : DomainEvent<LinkDetailsUpdatedEvent>, IDomainEventDescriptor, ILinkEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public LinkDetailsUpdatedEvent(
        Guid id,
        string ownerType,
        Guid ownerId,
        LinkDetails details,
        LinkDetails previous,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        OwnerType = ownerType;
        OwnerId = ownerId;
        Details = details;
        Previous = previous;

        Timestamp = timestamp;
    }

    /// <summary>The link's id.</summary>
    public Guid Id { get; }

    /// <inheritdoc/>
    public string OwnerType { get; }

    /// <inheritdoc/>
    public Guid OwnerId { get; }

    /// <summary>The details after the edit.</summary>
    public LinkDetails Details { get; }

    /// <summary>The details the edit replaced.</summary>
    public LinkDetails Previous { get; }

    [JsonIgnore]
    public string AggregateType => "Link";

    [JsonIgnore]
    public Guid AggregateId => Id;

    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => LinkEventRelations.For(OwnerType, OwnerId);
}

/// <summary>What a link is called and where it goes.</summary>
/// <param name="Name">What the link is called.</param>
/// <param name="Url">The address the link opens.</param>
public sealed record LinkDetails(string Name, string Url);
