using System.ComponentModel.DataAnnotations;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Discussions;

namespace Wayd.Common.Application.Discussions.Dtos;

/// <summary>
/// A record's conversation as the caller may use it: what they may do in it, and whether it is locked.
/// </summary>
public sealed record DiscussionDto
{
    /// <summary>Whether the caller may post and reply.</summary>
    public bool CanPost { get; init; }

    /// <summary>
    /// Whether the caller may post internal notes. When false, a composer offers no internal-note control at all.
    /// </summary>
    public bool CanPostInternal { get; init; }

    /// <summary>Whether the caller may lock the conversation and delete anyone's message.</summary>
    public bool CanModerate { get; init; }

    /// <summary>Whether the conversation is locked: nothing new can be posted, replied or edited.</summary>
    public bool IsLocked { get; init; }
}

/// <summary>What one entry in a conversation is.</summary>
public enum DiscussionEntryType
{
    /// <summary>A top-level message; see <see cref="DiscussionEntryDto.Message"/>.</summary>
    [Display(Name = "Message", Order = 1)]
    Message = 1,

    /// <summary>A change to the record, from its activity log; see <see cref="DiscussionEntryDto.Activity"/>.</summary>
    [Display(Name = "Activity", Order = 2)]
    Activity = 2,
}

/// <summary>One entry in a conversation: a top-level message, or a change to the record between messages.</summary>
public sealed record DiscussionEntryDto
{
    /// <summary>Which of <see cref="Message"/> and <see cref="Activity"/> is set.</summary>
    public DiscussionEntryType Type { get; init; }

    /// <summary>When the message was posted or the change happened; the conversation's order.</summary>
    public Instant Timestamp { get; init; }

    /// <summary>The message, when <see cref="Type"/> is <see cref="DiscussionEntryType.Message"/>.</summary>
    public DiscussionMessageDto? Message { get; init; }

    /// <summary>
    /// The change, when <see cref="Type"/> is <see cref="DiscussionEntryType.Activity"/>, exactly as the record's own
    /// Activity section shows it to the caller.
    /// </summary>
    public ActivityLogDto? Activity { get; init; }
}

/// <summary>A message in a conversation, top-level or a reply, as the caller may see it.</summary>
public sealed record DiscussionMessageDto
{
    /// <summary>The message's id.</summary>
    public Guid Id { get; init; }

    /// <summary>The top-level message whose thread this is a reply in, or null for a top-level message.</summary>
    public Guid? ParentMessageId { get; init; }

    /// <summary>Who the message is for.</summary>
    public DiscussionMessageKind Kind { get; init; }

    /// <summary>The optional subject of a top-level message. Null on a reply, and once deleted.</summary>
    public string? Subject { get; init; }

    /// <summary>
    /// The message as markdown, mentions written <c>@[Name](employee:{id})</c>; show each mention by the current name
    /// in <see cref="Mentions"/>. Null once deleted.
    /// </summary>
    public string? Body { get; init; }

    /// <summary>Who wrote the message.</summary>
    public required EmployeeNavigationDto Author { get; init; }

    /// <summary>When the message was posted.</summary>
    public Instant PostedOn { get; init; }

    /// <summary>When the message was last edited, or null if it never was.</summary>
    public Instant? EditedOn { get; init; }

    /// <summary>Whether the message was deleted, leaving only who wrote it, who deleted it and when.</summary>
    public bool IsDeleted { get; init; }

    /// <summary>When the message was deleted, or null while it stands.</summary>
    public Instant? DeletedOn { get; init; }

    /// <summary>Who deleted the message, or null while it stands.</summary>
    public EmployeeNavigationDto? DeletedBy { get; init; }

    /// <summary>The people the message mentions, by their current names.</summary>
    public List<EmployeeNavigationDto> Mentions { get; init; } = [];

    /// <summary>How many replies the thread this message starts holds. Zero on a reply.</summary>
    public int ReplyCount { get; init; }

    /// <summary>When the latest reply in the thread was posted, or null when there are none.</summary>
    public Instant? LastReplyOn { get; init; }

    /// <summary>Up to three people who replied most recently in the thread, newest first.</summary>
    public List<EmployeeNavigationDto> RecentRepliers { get; init; } = [];

    /// <summary>Whether the caller may edit the message: their own, standing, in an unlocked conversation.</summary>
    public bool CanEdit { get; init; }

    /// <summary>Whether the caller may delete the message: their own, or anyone's as a moderator.</summary>
    public bool CanDelete { get; init; }
}

/// <summary>A version of a message that an edit replaced.</summary>
public sealed record DiscussionMessageRevisionDto
{
    /// <summary>The subject before the edit, or null when there was none.</summary>
    public string? Subject { get; init; }

    /// <summary>The text before the edit, as markdown.</summary>
    public required string Body { get; init; }

    /// <summary>When the edit replaced this version.</summary>
    public Instant ReplacedOn { get; init; }
}
