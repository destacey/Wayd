using NodaTime;

namespace Wayd.Common.Domain.Discussions;

/// <summary>
/// A version of a message that an edit replaced, kept so a conversation can never be quietly rewritten.
/// </summary>
/// <remarks>
/// Read under the same rules as the message itself: a revision of an internal note is an internal note. Cleared
/// with the message when it is deleted. Changed only through <see cref="DiscussionMessage"/>.
/// </remarks>
public sealed class DiscussionMessageRevision
{
    private DiscussionMessageRevision() { }

    internal DiscussionMessageRevision(Guid messageId, string? subject, string body, Instant replacedOn)
    {
        MessageId = messageId;
        Subject = subject;
        Body = body;
        ReplacedOn = replacedOn;
    }

    /// <summary>The revision's id, assigned by the database in the order revisions are written.</summary>
    public long Id { get; private init; }

    /// <summary>The message this was a version of.</summary>
    public Guid MessageId { get; private init; }

    /// <summary>The subject before the edit, or null when there was none.</summary>
    public string? Subject { get; private init; }

    /// <summary>The text before the edit, as markdown.</summary>
    public string Body { get; private init; } = default!;

    /// <summary>When the edit replaced this version.</summary>
    public Instant ReplacedOn { get; private init; }
}
