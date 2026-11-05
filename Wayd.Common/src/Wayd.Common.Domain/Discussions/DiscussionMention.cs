using NodaTime;

namespace Wayd.Common.Domain.Discussions;

/// <summary>
/// One employee a message mentions.
/// </summary>
/// <remarks>
/// Stored beside the markdown rather than only inside it, so an edit can tell who it newly mentions and a later
/// "mentions of me" view is a query. A mention grants nothing by itself: whether it adds the person to the record is
/// the channel type's decision, made when the message is posted. Changed only through <see cref="DiscussionMessage"/>.
/// </remarks>
public sealed class DiscussionMention
{
    private DiscussionMention() { }

    internal DiscussionMention(Guid messageId, Guid employeeId, Instant mentionedOn)
    {
        MessageId = messageId;
        EmployeeId = employeeId;
        MentionedOn = mentionedOn;
    }

    /// <summary>The message that mentions the employee.</summary>
    public Guid MessageId { get; private init; }

    /// <summary>The employee mentioned.</summary>
    public Guid EmployeeId { get; private init; }

    /// <summary>When the mention was made: when the message was posted, or the edit that added it.</summary>
    public Instant MentionedOn { get; private init; }
}
