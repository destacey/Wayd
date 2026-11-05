namespace Wayd.Common.Domain.Discussions;

/// <summary>
/// The failures discussions report, shared by the domain, the writer and their tests.
/// </summary>
public static class DiscussionErrors
{
    /// <summary>No channel type was given.</summary>
    public const string ChannelTypeRequired = "A discussion channel type is required.";

    /// <summary>A channel type no module registered.</summary>
    public static string UnknownChannelType(string key) => $"'{key}' is not a kind of record that can be discussed.";

    /// <summary>Posting, replying or editing in a locked conversation.</summary>
    public const string ChannelLocked = "This conversation is locked.";

    /// <summary>Locking a conversation that is already locked.</summary>
    public const string AlreadyLocked = "This conversation is already locked.";

    /// <summary>Unlocking a conversation that is not locked.</summary>
    public const string NotLocked = "This conversation is not locked.";

    /// <summary>A message with no text.</summary>
    public const string BodyRequired = "A message needs some text.";

    /// <summary>A message longer than <see cref="DiscussionMessage.BodyMaxLength"/>.</summary>
    public const string BodyTooLong = "A message can be at most 10,000 characters.";

    /// <summary>A subject longer than <see cref="DiscussionMessage.SubjectMaxLength"/>.</summary>
    public const string SubjectTooLong = "A subject can be at most 200 characters.";

    /// <summary>A subject on a reply in a thread, or on a kind of record that has none.</summary>
    public const string SubjectNotAllowed = "Only a message that starts a conversation can have a subject.";

    /// <summary>An internal note on a kind of record that has none.</summary>
    public const string InternalNotesNotSupported = "Conversations on this kind of record cannot hold internal notes.";

    /// <summary>Saving settings for a kind of record whose conversations have none.</summary>
    public const string SettingsNotSupported = "Conversations on this kind of record have no settings.";

    /// <summary>Letting mentions grant access on a kind of record where a mention only notifies.</summary>
    public const string MentionAccessNotSupported = "On this kind of record a mention only notifies; it cannot give access.";

    /// <summary>Posting an internal note, or replying in one's thread, while internal notes are turned off.</summary>
    public const string InternalNotesTurnedOff = "Internal notes are turned off here.";

    /// <summary>A reply whose parent, or an edit or delete whose message, is in a different conversation.</summary>
    public const string MessageInAnotherChannel = "That message is in a different conversation.";

    /// <summary>A reply to a reply.</summary>
    public const string ReplyToReply = "Replies go one level deep. Reply to the message that started the thread.";

    /// <summary>Editing someone else's message.</summary>
    public const string NotTheAuthor = "Only the author can edit a message.";

    /// <summary>Editing or deleting a message that has been deleted.</summary>
    public const string MessageDeleted = "This message has been deleted.";
}
