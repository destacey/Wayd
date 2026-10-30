using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Events.Discussions;

namespace Wayd.Common.Domain.Discussions;

/// <summary>
/// One message in a conversation: a top-level message, which may start a thread, or a reply in one.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Threads go one level deep.</strong> A reply's parent is always a top-level message in the same channel,
/// and a reply takes its parent's <see cref="Kind"/>, so a thread is wholly replies or wholly internal notes and the
/// count of its replies never reveals a note to someone who cannot read it.
/// </para>
/// <para>
/// <strong>Never rewritten quietly.</strong> Every edit keeps what it replaced as a revision, and deleting leaves a
/// tombstone — who deleted it and when — in place of the text. Deliberately not <c>ISystemAuditable</c>: the
/// revisions are its history, and a row audit would copy an internal note's text into the audit trail.
/// </para>
/// <para>
/// <strong>A tombstone, not a soft delete.</strong> <see cref="Delete"/> destroys the text and keeps the row visible,
/// so the conversation shows where a message was and its replies still read. It is deliberately not
/// <c>ISoftDelete</c>, whose query filter would hide the row and keep the text. Never <c>Remove</c> a message: rows
/// leave only with their channel, when the record the conversation is about is deleted.
/// </para>
/// <para>
/// Who may post, edit or delete is the channel type's answer and the writer asks it first. The rules here are the
/// ones that hold whoever is asking: the thread's shape, the lengths, the lock, and that only the author edits.
/// </para>
/// </remarks>
public sealed class DiscussionMessage : BaseEntity
{
    /// <summary>The longest subject a top-level message may carry.</summary>
    public const int SubjectMaxLength = 200;

    /// <summary>The longest message body.</summary>
    public const int BodyMaxLength = 10_000;

    private readonly List<DiscussionMessageRevision> _revisions = [];
    private readonly List<DiscussionMention> _mentions = [];

    private DiscussionMessage() { }

    private DiscussionMessage(
        Guid channelId,
        Guid? parentMessageId,
        DiscussionMessageKind kind,
        string? subject,
        string body,
        Guid authorEmployeeId,
        Instant postedOn)
    {
        ChannelId = channelId;
        ParentMessageId = parentMessageId;
        Kind = kind;
        Subject = subject;
        Body = body;
        AuthorEmployeeId = authorEmployeeId;
        PostedOn = postedOn;
    }

    /// <summary>The conversation the message belongs to.</summary>
    public Guid ChannelId { get; private init; }

    /// <summary>
    /// The domain event of the change to the record that the message was posted with, in the same save, such as the
    /// status change of asking a requester a question. Null for a message posted on its own.
    /// </summary>
    /// <remarks>
    /// The event's id, which keys its activity log entry, so a conversation shows the change on the message rather than
    /// as an entry of its own. Never shown by itself: the record's read rule still decides who may see the change.
    /// </remarks>
    public Guid? RecordChangeEventId { get; private init; }

    /// <summary>The top-level message whose thread this is a reply in, or null for a top-level message.</summary>
    public Guid? ParentMessageId { get; private init; }

    /// <summary>Who the message is for. Never changes after posting.</summary>
    public DiscussionMessageKind Kind { get; private init; }

    /// <summary>The optional subject of a top-level message. Always null on a reply, and once deleted.</summary>
    public string? Subject { get; private set; }

    /// <summary>The message as markdown. Null once deleted.</summary>
    public string? Body { get; private set; }

    /// <summary>The employee who wrote the message.</summary>
    public Guid AuthorEmployeeId { get; private init; }

    /// <summary>When the message was posted.</summary>
    public Instant PostedOn { get; private init; }

    /// <summary>When the message was last edited, or null if it never was.</summary>
    public Instant? EditedOn { get; private set; }

    /// <summary>When the message was deleted, or null while it stands.</summary>
    public Instant? DeletedOn { get; private set; }

    /// <summary>The employee who deleted the message, or null while it stands.</summary>
    public Guid? DeletedById { get; private set; }

    /// <summary>Whether the message has been deleted, leaving only its tombstone.</summary>
    public bool IsDeleted => DeletedOn is not null;

    /// <summary>Whether the message starts a conversation rather than replying in a thread.</summary>
    public bool IsTopLevel => ParentMessageId is null;

    /// <summary>Every version an edit replaced, oldest first. Empty once deleted.</summary>
    public IReadOnlyCollection<DiscussionMessageRevision> Revisions => _revisions.AsReadOnly();

    /// <summary>The employees the message mentions. Empty once deleted.</summary>
    public IReadOnlyCollection<DiscussionMention> Mentions => _mentions.AsReadOnly();

    /// <summary>
    /// Posts a top-level message to a conversation.
    /// </summary>
    /// <param name="channel">The conversation, which must not be locked.</param>
    /// <param name="kind">Who the message is for. An internal note needs a channel type that supports them.</param>
    /// <param name="authorEmployeeId">The employee writing the message.</param>
    /// <param name="subject">An optional subject; blank means none.</param>
    /// <param name="body">The message as markdown.</param>
    /// <param name="mentionedEmployeeIds">
    /// The employees the message mentions, already narrowed to those the author may mention. The author is dropped.
    /// </param>
    /// <param name="recordChangeEventId">
    /// The event of a change to the record saved with the message, or null; see <see cref="RecordChangeEventId"/>.
    /// </param>
    public static Result<DiscussionMessage> Post(
        DiscussionChannel channel,
        DiscussionMessageKind kind,
        Guid authorEmployeeId,
        string? subject,
        string body,
        IEnumerable<Guid> mentionedEmployeeIds,
        EventActor actor,
        Instant timestamp,
        Guid? recordChangeEventId = null)
    {
        Guard.Against.Null(channel, nameof(channel));
        Guard.Against.EnumOutOfRange(kind, nameof(kind));
        Guard.Against.Default(authorEmployeeId, nameof(authorEmployeeId));

        var descriptor = DiscussionChannelTypes.Resolve(channel.ChannelType);
        if (descriptor.IsFailure)
        {
            return Result.Failure<DiscussionMessage>(descriptor.Error);
        }

        if (channel.IsLocked)
        {
            return Result.Failure<DiscussionMessage>(DiscussionErrors.ChannelLocked);
        }

        if (kind == DiscussionMessageKind.InternalNote && !descriptor.Value.SupportsInternalNotes)
        {
            return Result.Failure<DiscussionMessage>(DiscussionErrors.InternalNotesNotSupported);
        }

        var text = Normalize(subject, body, isTopLevel: true, descriptor.Value);
        if (text.IsFailure)
        {
            return Result.Failure<DiscussionMessage>(text.Error);
        }

        var message = new DiscussionMessage(channel.Id, null, kind, text.Value.Subject, text.Value.Body, authorEmployeeId, timestamp)
        {
            RecordChangeEventId = recordChangeEventId,
        };
        message.AddMentions(mentionedEmployeeIds, timestamp);
        message.RaisePosted(channel, actor, timestamp);

        return message;
    }

    /// <summary>
    /// Replies in the thread a top-level message starts. The reply takes that message's kind and has no subject.
    /// </summary>
    /// <param name="channel">The conversation, which must not be locked.</param>
    /// <param name="parent">The top-level message starting the thread, in <paramref name="channel"/>.</param>
    /// <param name="authorEmployeeId">The employee writing the reply.</param>
    /// <param name="body">The reply as markdown.</param>
    /// <param name="mentionedEmployeeIds">
    /// The employees the reply mentions, already narrowed to those the author may mention. The author is dropped.
    /// </param>
    public static Result<DiscussionMessage> Reply(
        DiscussionChannel channel,
        DiscussionMessage parent,
        Guid authorEmployeeId,
        string body,
        IEnumerable<Guid> mentionedEmployeeIds,
        EventActor actor,
        Instant timestamp)
    {
        Guard.Against.Null(channel, nameof(channel));
        Guard.Against.Null(parent, nameof(parent));
        Guard.Against.Default(authorEmployeeId, nameof(authorEmployeeId));

        var descriptor = DiscussionChannelTypes.Resolve(channel.ChannelType);
        if (descriptor.IsFailure)
        {
            return Result.Failure<DiscussionMessage>(descriptor.Error);
        }

        if (parent.ChannelId != channel.Id)
        {
            return Result.Failure<DiscussionMessage>(DiscussionErrors.MessageInAnotherChannel);
        }

        if (!parent.IsTopLevel)
        {
            return Result.Failure<DiscussionMessage>(DiscussionErrors.ReplyToReply);
        }

        if (channel.IsLocked)
        {
            return Result.Failure<DiscussionMessage>(DiscussionErrors.ChannelLocked);
        }

        var text = Normalize(null, body, isTopLevel: false, descriptor.Value);
        if (text.IsFailure)
        {
            return Result.Failure<DiscussionMessage>(text.Error);
        }

        var reply = new DiscussionMessage(channel.Id, parent.Id, parent.Kind, null, text.Value.Body, authorEmployeeId, timestamp);
        reply.AddMentions(mentionedEmployeeIds, timestamp);
        reply.RaisePosted(channel, actor, timestamp);

        return reply;
    }

    /// <summary>
    /// Changes the message's subject, text or mentions, keeping the version it replaces. Only the author may edit,
    /// and nothing is recorded when nothing changed.
    /// </summary>
    /// <param name="channel">The conversation the message is in, which must not be locked.</param>
    /// <param name="editorEmployeeId">The employee editing, who must be the author.</param>
    /// <param name="subject">The new subject; blank means none. Must be blank on a reply.</param>
    /// <param name="body">The new text as markdown.</param>
    /// <param name="mentionedEmployeeIds">
    /// Every employee the edited message mentions, already narrowed to those the author may mention.
    /// </param>
    public Result Edit(
        DiscussionChannel channel,
        Guid editorEmployeeId,
        string? subject,
        string body,
        IEnumerable<Guid> mentionedEmployeeIds,
        EventActor actor,
        Instant timestamp)
    {
        Guard.Against.Null(channel, nameof(channel));

        if (channel.Id != ChannelId)
        {
            return Result.Failure(DiscussionErrors.MessageInAnotherChannel);
        }

        if (IsDeleted)
        {
            return Result.Failure(DiscussionErrors.MessageDeleted);
        }

        if (editorEmployeeId != AuthorEmployeeId)
        {
            return Result.Failure(DiscussionErrors.NotTheAuthor);
        }

        if (channel.IsLocked)
        {
            return Result.Failure(DiscussionErrors.ChannelLocked);
        }

        var descriptor = DiscussionChannelTypes.Resolve(channel.ChannelType);
        if (descriptor.IsFailure)
        {
            return Result.Failure(descriptor.Error);
        }

        var text = Normalize(subject, body, IsTopLevel, descriptor.Value);
        if (text.IsFailure)
        {
            return Result.Failure(text.Error);
        }

        var previousSubject = Subject;
        var previousBody = Body!;
        var previousMentions = _mentions.Select(m => m.EmployeeId).ToHashSet();

        Subject = text.Value.Subject;
        Body = text.Value.Body;
        var textChanged = Subject != previousSubject || Body != previousBody;

        var mentions = MentionSet(mentionedEmployeeIds);
        Guid[] added = [.. mentions.Where(id => !previousMentions.Contains(id))];
        Guid[] removed = [.. previousMentions.Where(id => !mentions.Contains(id))];

        if (!textChanged && added.Length == 0 && removed.Length == 0)
        {
            return Result.Success();
        }

        if (textChanged)
        {
            _revisions.Add(new DiscussionMessageRevision(Id, previousSubject, previousBody, timestamp));
            EditedOn = timestamp;
        }

        _mentions.RemoveAll(m => removed.Contains(m.EmployeeId));
        _mentions.AddRange(added.Select(id => new DiscussionMention(Id, id, timestamp)));

        AddDomainEvent(new DiscussionMessageEditedEvent(
            Id, ChannelId, channel.ChannelType, channel.OwnerId, textChanged, added, removed, MentionedIds(), actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Deletes the message: its subject, text, earlier versions and mentions are cleared and a tombstone stays.
    /// Whether the employee may delete it — its author, or a moderator — is the channel type's answer, asked first.
    /// </summary>
    /// <param name="channel">The conversation the message is in.</param>
    /// <param name="deletedById">The employee deleting the message.</param>
    public Result Delete(DiscussionChannel channel, Guid deletedById, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(channel, nameof(channel));
        Guard.Against.Default(deletedById, nameof(deletedById));

        if (channel.Id != ChannelId)
        {
            return Result.Failure(DiscussionErrors.MessageInAnotherChannel);
        }

        if (IsDeleted)
        {
            return Result.Failure(DiscussionErrors.MessageDeleted);
        }

        Subject = null;
        Body = null;
        _revisions.Clear();
        _mentions.Clear();
        DeletedOn = timestamp;
        DeletedById = deletedById;

        AddDomainEvent(new DiscussionMessageDeletedEvent(Id, ChannelId, channel.ChannelType, channel.OwnerId, deletedById, actor, timestamp));

        return Result.Success();
    }

    private void AddMentions(IEnumerable<Guid> mentionedEmployeeIds, Instant timestamp) =>
        _mentions.AddRange(MentionSet(mentionedEmployeeIds).Select(id => new DiscussionMention(Id, id, timestamp)));

    private void RaisePosted(DiscussionChannel channel, EventActor actor, Instant timestamp) =>
        AddDomainEvent(new DiscussionMessagePostedEvent(
            Id, ChannelId, channel.ChannelType, channel.OwnerId, ParentMessageId, Kind, AuthorEmployeeId, MentionedIds(), actor, timestamp));

    private Guid[] MentionedIds() => [.. _mentions.Select(m => m.EmployeeId)];

    /// <summary>The distinct, non-empty mentions, without the author: mentioning yourself tells nobody anything.</summary>
    private HashSet<Guid> MentionSet(IEnumerable<Guid> mentionedEmployeeIds) =>
        [.. (mentionedEmployeeIds ?? []).Where(id => id != Guid.Empty && id != AuthorEmployeeId)];

    private static Result<(string? Subject, string Body)> Normalize(
        string? subject,
        string body,
        bool isTopLevel,
        DiscussionChannelTypeDescriptor descriptor)
    {
        var trimmedBody = body?.Trim();
        if (string.IsNullOrEmpty(trimmedBody))
        {
            return Result.Failure<(string?, string)>(DiscussionErrors.BodyRequired);
        }

        if (trimmedBody.Length > BodyMaxLength)
        {
            return Result.Failure<(string?, string)>(DiscussionErrors.BodyTooLong);
        }

        var trimmedSubject = string.IsNullOrWhiteSpace(subject) ? null : subject.Trim();
        if (trimmedSubject is not null)
        {
            if (!isTopLevel || !descriptor.SupportsSubjects)
            {
                return Result.Failure<(string?, string)>(DiscussionErrors.SubjectNotAllowed);
            }

            if (trimmedSubject.Length > SubjectMaxLength)
            {
                return Result.Failure<(string?, string)>(DiscussionErrors.SubjectTooLong);
            }
        }

        return (trimmedSubject, trimmedBody);
    }
}
