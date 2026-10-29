using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Discussions;

/// <summary>
/// A message was deleted. Its text and earlier versions are gone; a tombstone naming who deleted it and when stays
/// in the conversation, so a thread can never be quietly rewritten.
/// </summary>
public sealed record DiscussionMessageDeletedEvent : DomainEvent<DiscussionMessageDeletedEvent>, IDomainEventDescriptor, IDiscussionEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    public DiscussionMessageDeletedEvent(Guid id, Guid channelId, string channelType, Guid? ownerId, Guid deletedById, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        ChannelId = channelId;
        ChannelType = channelType;
        OwnerId = ownerId;
        DeletedById = deletedById;

        Timestamp = timestamp;
    }

    /// <summary>The message's id.</summary>
    public Guid Id { get; }

    /// <summary>The conversation the message was in.</summary>
    public Guid ChannelId { get; }

    /// <inheritdoc/>
    public string ChannelType { get; }

    /// <inheritdoc/>
    public Guid? OwnerId { get; }

    /// <summary>The employee who deleted the message: its author, or a moderator.</summary>
    public Guid DeletedById { get; }

    [JsonIgnore]
    public string AggregateType => "DiscussionMessage";

    [JsonIgnore]
    public Guid AggregateId => Id;
}
