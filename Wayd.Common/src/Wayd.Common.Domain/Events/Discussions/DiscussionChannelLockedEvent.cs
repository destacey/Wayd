using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Discussions;

/// <summary>
/// A conversation was locked: nothing new can be posted, replied or edited until it is unlocked.
/// </summary>
public sealed record DiscussionChannelLockedEvent : DomainEvent<DiscussionChannelLockedEvent>, IDomainEventDescriptor, IDiscussionEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    public DiscussionChannelLockedEvent(Guid id, string channelType, Guid? ownerId, Guid lockedById, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        ChannelType = channelType;
        OwnerId = ownerId;
        LockedById = lockedById;

        Timestamp = timestamp;
    }

    /// <summary>The channel's id.</summary>
    public Guid Id { get; }

    /// <inheritdoc/>
    public string ChannelType { get; }

    /// <inheritdoc/>
    public Guid? OwnerId { get; }

    /// <summary>The employee who locked the conversation.</summary>
    public Guid LockedById { get; }

    [JsonIgnore]
    public string AggregateType => "DiscussionChannel";

    [JsonIgnore]
    public Guid AggregateId => Id;
}
