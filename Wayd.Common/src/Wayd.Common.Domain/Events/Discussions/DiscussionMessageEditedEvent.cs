using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Discussions;

/// <summary>
/// A message's author changed its subject or text, or who it mentions.
/// </summary>
/// <remarks>
/// Neither end of the text is carried: the message keeps every version it replaced as a revision, read through the
/// conversation's own rules, which an event payload would bypass. The mentions carry both ends, so a handler can
/// tell only the people an edit newly mentions.
/// </remarks>
public sealed record DiscussionMessageEditedEvent : DomainEvent<DiscussionMessageEditedEvent>, IDomainEventDescriptor, IDiscussionEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public DiscussionMessageEditedEvent(
        Guid id,
        Guid channelId,
        string channelType,
        Guid? ownerId,
        bool textChanged,
        IReadOnlyList<Guid> addedMentionIds,
        IReadOnlyList<Guid> removedMentionIds,
        IReadOnlyList<Guid> mentionedEmployeeIds,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        ChannelId = channelId;
        ChannelType = channelType;
        OwnerId = ownerId;
        TextChanged = textChanged;
        AddedMentionIds = addedMentionIds;
        RemovedMentionIds = removedMentionIds;
        MentionedEmployeeIds = mentionedEmployeeIds;

        Timestamp = timestamp;
    }

    /// <summary>The message's id.</summary>
    public Guid Id { get; }

    /// <summary>The conversation the message is in.</summary>
    public Guid ChannelId { get; }

    /// <inheritdoc/>
    public string ChannelType { get; }

    /// <inheritdoc/>
    public Guid? OwnerId { get; }

    /// <summary>Whether the subject or text changed, and so a revision was kept.</summary>
    public bool TextChanged { get; }

    /// <summary>The employees the edit newly mentions.</summary>
    public IReadOnlyList<Guid> AddedMentionIds { get; }

    /// <summary>The employees the edit no longer mentions.</summary>
    public IReadOnlyList<Guid> RemovedMentionIds { get; }

    /// <summary>Every employee the message mentions after the edit.</summary>
    public IReadOnlyList<Guid> MentionedEmployeeIds { get; }

    [JsonIgnore]
    public string AggregateType => "DiscussionMessage";

    [JsonIgnore]
    public Guid AggregateId => Id;
}
