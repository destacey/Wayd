using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Discussions;

/// <summary>
/// A locked conversation was unlocked, so people can post again.
/// </summary>
public sealed record DiscussionChannelUnlockedEvent : DomainEvent<DiscussionChannelUnlockedEvent>, IDomainEventDescriptor, IDiscussionEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    public DiscussionChannelUnlockedEvent(Guid id, string channelType, Guid? ownerId, EventActor actor, Instant timestamp)
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
