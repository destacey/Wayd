using Wayd.Common.Domain.Discussions;

namespace Wayd.Common.Application.Discussions;

/// <summary>
/// The only way to change a conversation: post, reply, edit, delete and lock, each asked of the record's channel
/// type first.
/// </summary>
/// <remarks>
/// <para>
/// The discussion endpoints, a module's own commands (a status change that also asks the requester a question) and
/// the MCP tools all come through here, so the rules for who may write what live in one place.
/// </para>
/// <para>
/// <strong>It stages and never saves.</strong> The caller saves, so a module can change its own record and post in
/// the same transaction. A record the caller cannot see, and a message they cannot read, fail with
/// <see cref="DiscussionAccessErrors.NotFound"/>, never a permission error, so a conversation never confirms that a
/// hidden note exists.
/// </para>
/// </remarks>
public interface IDiscussionWriter : IScopedService
{
    /// <summary>
    /// Posts a top-level message on a record, opening its conversation on the first post. Returns the message's id.
    /// </summary>
    Task<Result<Guid>> Post(PostDiscussionMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Replies in the thread a top-level message starts. The reply takes that message's kind. Returns its id.
    /// </summary>
    Task<Result<Guid>> Reply(string channelType, Guid ownerId, Guid parentMessageId, string body, CancellationToken cancellationToken);

    /// <summary>Edits the caller's own message, keeping the version it replaces.</summary>
    Task<Result> Edit(string channelType, Guid ownerId, Guid messageId, string? subject, string body, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a message, leaving a tombstone: the author may delete their own, a moderator anyone's.
    /// </summary>
    Task<Result> Delete(string channelType, Guid ownerId, Guid messageId, CancellationToken cancellationToken);

    /// <summary>
    /// Locks or unlocks a record's conversation. Moderators only. Locking a record nobody has posted on yet opens its
    /// conversation locked.
    /// </summary>
    Task<Result> SetLocked(string channelType, Guid ownerId, bool locked, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a record's conversation and every message in it, for a module hard-deleting the record. Asks no
    /// access: the module has already decided the record may go. Does nothing when there is no conversation.
    /// </summary>
    Task DeleteChannel(string channelType, Guid ownerId, CancellationToken cancellationToken);
}

/// <summary>A top-level message to post on a record.</summary>
/// <param name="ChannelType">The record's registered channel type.</param>
/// <param name="OwnerId">The record's id.</param>
/// <param name="Kind">Who the message is for.</param>
/// <param name="Subject">An optional subject; blank means none.</param>
/// <param name="Body">The message as markdown, mentions written <c>@[Name](employee:{id})</c>.</param>
public sealed record PostDiscussionMessage(string ChannelType, Guid OwnerId, DiscussionMessageKind Kind, string? Subject, string Body);
