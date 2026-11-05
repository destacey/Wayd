namespace Wayd.Web.Api.Models.Discussions;

/// <summary>
/// The new subject and text of the caller's own message. The version it replaces is kept.
/// </summary>
public sealed record EditDiscussionMessageRequest
{
    /// <summary>The new subject, up to 200 characters; null or blank for none. Must be blank on a reply in a thread.</summary>
    public string? Subject { get; set; }

    /// <summary>
    /// The new text as markdown, up to 10,000 characters. Mention someone as `@[Name](employee:{employeeId})`.
    /// </summary>
    public string Body { get; set; } = default!;
}
