namespace Wayd.Web.Api.Models.Discussions;

/// <summary>
/// A reply in the thread a top-level message starts. It takes that message's kind and has no subject.
/// </summary>
public sealed record ReplyToDiscussionMessageRequest
{
    /// <summary>
    /// The reply as markdown, up to 10,000 characters. Mention someone as `@[Name](employee:{employeeId})`.
    /// </summary>
    public string Body { get; set; } = default!;
}
