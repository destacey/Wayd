namespace Wayd.Common.Application.Discussions;

/// <summary>
/// The failures the writer and queries report when the caller may not do something, beside the domain's own rules
/// in <c>DiscussionErrors</c>.
/// </summary>
public static class DiscussionAccessErrors
{
    /// <summary>
    /// The record, its conversation or the message does not exist, or the caller may not see it. An endpoint reports
    /// it as 404, exactly as a record that does not exist, so a conversation can never confirm a hidden note.
    /// </summary>
    public const string NotFound = "Conversation or message not found.";

    /// <summary>Posting, replying or editing without being allowed to write that kind of message.</summary>
    public const string CannotWrite = "You cannot post this kind of message here.";

    /// <summary>Deleting someone else's message, or locking, without moderating the conversation.</summary>
    public const string CannotModerate = "Only the conversation's moderators can do this.";

    /// <summary>Whether a failure is <see cref="NotFound"/>.</summary>
    public static bool IsNotFound(string? error) => error == NotFound;

    /// <summary>Whether a failure is the caller lacking permission rather than breaking a rule.</summary>
    public static bool IsForbidden(string? error) => error is CannotWrite or CannotModerate;
}
