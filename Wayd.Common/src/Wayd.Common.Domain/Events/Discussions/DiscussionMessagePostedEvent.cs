using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Discussions;

namespace Wayd.Common.Domain.Events.Discussions;

/// <summary>
/// A message was posted to a conversation: a new top-level message, or a reply in a thread.
/// </summary>
/// <remarks>
/// Carries who to tell and why — the kind, the thread and who was mentioned — never what was said. A handler that
/// needs the text reads the message through the conversation's own rules.
/// </remarks>
public sealed record DiscussionMessagePostedEvent : DomainEvent<DiscussionMessagePostedEvent>, IDomainEventDescriptor, IDiscussionEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    public DiscussionMessagePostedEvent(
        Guid id,
        Guid channelId,
        string channelType,
        Guid? ownerId,
        Guid? parentMessageId,
        DiscussionMessageKind kind,
        Guid authorEmployeeId,
        IReadOnlyList<Guid> mentionedEmployeeIds,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        ChannelId = channelId;
        ChannelType = channelType;
        OwnerId = ownerId;
        ParentMessageId = parentMessageId;
        Kind = kind;
        AuthorEmployeeId = authorEmployeeId;
        MentionedEmployeeIds = mentionedEmployeeIds;

        Timestamp = timestamp;
    }

    /// <summary>The message's id.</summary>
    public Guid Id { get; }

    /// <summary>The conversation the message was posted to.</summary>
    public Guid ChannelId { get; }

    /// <inheritdoc/>
    public string ChannelType { get; }

    /// <inheritdoc/>
    public Guid? OwnerId { get; }

    /// <summary>The message that started the thread this is a reply in, or null for a top-level message.</summary>
    public Guid? ParentMessageId { get; }

    /// <summary>Who the message is for.</summary>
    public DiscussionMessageKind Kind { get; }

    /// <summary>The employee who wrote the message.</summary>
    public Guid AuthorEmployeeId { get; }

    /// <summary>The employees the message mentions, excluding its author.</summary>
    public IReadOnlyList<Guid> MentionedEmployeeIds { get; }

    [JsonIgnore]
    public string AggregateType => "DiscussionMessage";

    [JsonIgnore]
    public Guid AggregateId => Id;
}
