using Wayd.Common.Domain.Discussions;

namespace Wayd.Web.Api.Models.Discussions;

/// <summary>
/// A message that starts a conversation thread on a record.
/// </summary>
public sealed record PostDiscussionMessageRequest
{
    /// <summary>
    /// Who the message is for: `Reply` for everyone who can read the conversation, `InternalNote` for its internal
    /// audience only, such as an intake desk's agents.
    /// </summary>
    public DiscussionMessageKind Kind { get; set; } = DiscussionMessageKind.Reply;

    /// <summary>An optional subject of up to 200 characters, so parallel topics on a busy record stay apart.</summary>
    public string? Subject { get; set; }

    /// <summary>
    /// The message as markdown, up to 10,000 characters. Mention someone as `@[Name](employee:{employeeId})`.
    /// </summary>
    public string Body { get; set; } = default!;
}
