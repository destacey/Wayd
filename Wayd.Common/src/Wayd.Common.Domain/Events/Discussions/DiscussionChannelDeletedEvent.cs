using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Discussions;

/// <summary>
/// A record's conversation was removed with every message in it, because the record it was about was deleted.
/// </summary>
public sealed record DiscussionChannelDeletedEvent : DomainEvent<DiscussionChannelDeletedEvent>, IDomainEventDescriptor, IDiscussionEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    public DiscussionChannelDeletedEvent(Guid id, string channelType, Guid? ownerId, EventActor actor, Instant timestamp)
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
