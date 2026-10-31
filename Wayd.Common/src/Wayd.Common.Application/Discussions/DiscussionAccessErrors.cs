namespace Wayd.Common.Application.Discussions;

/// <summary>
/// The failures the writer and queries report when the caller may not do something, or collided with someone else,
/// beside the domain's own rules in <c>DiscussionErrors</c>.
/// </summary>
/// <remarks>
/// An endpoint maps these by value: <see cref="NotFound"/> to 404, those <see cref="IsForbidden"/> names to 403,
/// <see cref="Conflict"/> to 409, and anything else to 400. A new access failure must be added to
/// <see cref="IsForbidden"/>, or it is reported as a broken rule.
/// </remarks>
public static class DiscussionAccessErrors
{
    /// <summary>
    /// The record, its conversation or the message does not exist, or the caller may not see it. An endpoint reports
    /// it as 404, exactly as a record that does not exist, so a conversation can never confirm a hidden note.
    /// </summary>
    public const string NotFound = "Conversation or message not found.";

    /// <summary>Posting, replying or editing without being allowed to write that kind of message.</summary>
    public const string CannotWrite = "You cannot post this kind of message here.";

    /// <summary>Editing someone else's message.</summary>
    public const string NotTheAuthor = "Only the author can edit a message.";

    /// <summary>Deleting someone else's message, or locking, without moderating the conversation.</summary>
    public const string CannotModerate = "Only the conversation's moderators can do this.";

    /// <summary>Changing a scope's discussion settings without running it.</summary>
    public const string CannotManageSettings = "Only the people who run this can change how its conversations work.";

    /// <summary>
    /// Someone else changed the conversation or the message in the moment between reading and saving: two first posts
    /// on a record, or an edit and a delete of one message, at once.
    /// </summary>
    public const string Conflict = "Someone else changed this conversation at the same moment. Reload it and try again.";

    /// <summary>Whether a failure is <see cref="NotFound"/>.</summary>
    public static bool IsNotFound(string? error) => error == NotFound;

    /// <summary>Whether a failure is the caller lacking permission rather than breaking a rule.</summary>
    public static bool IsForbidden(string? error) => error is CannotWrite or NotTheAuthor or CannotModerate or CannotManageSettings;

    /// <summary>Whether a failure is <see cref="Conflict"/>.</summary>
    public static bool IsConflict(string? error) => error == Conflict;
}
