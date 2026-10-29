using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Discussions;

/// <summary>
/// A record's conversation was opened, by the first message posted on it.
/// </summary>
public sealed record DiscussionChannelOpenedEvent : DomainEvent<DiscussionChannelOpenedEvent>, IDomainEventDescriptor, IDiscussionEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    public DiscussionChannelOpenedEvent(Guid id, string channelType, Guid? ownerId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        ChannelType = channelType;
        OwnerId = ownerId;

        Timestamp = timestamp;
    }

    /// <summary>The channel's id.</summary>
    public Guid Id { get; }

    /// <inheritdoc/>
    public string ChannelType { get; }

    /// <inheritdoc/>
    public Guid? OwnerId { get; }

    [JsonIgnore]
    public string AggregateType => "DiscussionChannel";

    [JsonIgnore]
    public Guid AggregateId => Id;
}
