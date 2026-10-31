using Wayd.Common.Domain.Discussions;

namespace Wayd.Common.Application.Discussions;

/// <summary>
/// How one kind of record answers who may read, post and moderate its conversation, and what posting means for
/// the record.
/// </summary>
/// <remarks>
/// The records and the rules for who may see them belong to the modules, so each module implements this once per
/// channel type, in its own Application project, and Infrastructure registers it. Every read and write of a
/// conversation asks it first, through <c>IDiscussionWriter</c> and the discussion queries, so no endpoint, module
/// command or MCP tool can disagree about who may see a message.
/// <para>
/// <strong>It decides from the record as it is now.</strong> Nothing about access is stored on a channel, so a
/// rerouted request or a moved project changes who may read its conversation the moment it moves.
/// </para>
/// <para>
/// <strong>Deliberately unmarked</strong>, like <c>IRecordReadRule</c>: the marker scan binds one implementation
/// per interface, so several channel types would collapse to one. Registration is explicit, and the implementation
/// must be <c>public</c>, because Wolverine's generated handlers construct it directly.
/// </para>
/// <para>
/// <strong>Its key must be the record's read rule owner type.</strong> A conversation shows the record's activity
/// through the <c>IRecordReadRule</c> registered under the same key; with none, it shows messages only.
/// </para>
/// </remarks>
public interface IDiscussionChannelType
{
    /// <summary>The registered channel type this answers for, spelled as its descriptor's key.</summary>
    string ChannelType { get; }

    /// <summary>
    /// What the caller may do in the conversation on the record, or null when they cannot read the record at all —
    /// or it does not exist — which every caller reports as not found.
    /// </summary>
    Task<DiscussionAccess?> Resolve(Guid ownerId, CancellationToken cancellationToken);

    /// <summary>
    /// Of the employees a message mentions, those its author may mention in a message of the given kind. A mention
    /// the author may not make is dropped, not refused, so a stale name in a draft never blocks a post.
    /// </summary>
    /// <remarks>
    /// Called only after <see cref="Resolve"/> allowed the post. A mention in an internal note should be limited to
    /// the internal audience, or the mention itself would tell someone a note exists.
    /// </remarks>
    Task<IReadOnlySet<Guid>> MentionableAmong(
        Guid ownerId,
        IReadOnlyCollection<Guid> employeeIds,
        DiscussionMessageKind kind,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies what posting means for the record — adding the author or a mentioned person as a participant, say —
    /// to the module's own aggregate, tracked in the same unit of work, before the caller saves.
    /// </summary>
    /// <remarks>
    /// In the same save rather than a handler of the posted event, because it can grant access: a person who has
    /// just been mentioned in, and so added to, a conversation must be able to open it at once. Notifications are
    /// not sent here; they follow from the event.
    /// </remarks>
    Task OnPosted(DiscussionChannel channel, DiscussionMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Applies what an edit means for the record, tracked in the same unit of work, before the caller saves. Called
    /// only when the edit added mentions, so a mention written in later reaches the record as one written at once.
    /// </summary>
    /// <param name="addedMentionIds">The employees the edit newly mentions, already narrowed by <see cref="MentionableAmong"/>.</param>
    Task OnEdited(
        DiscussionChannel channel,
        DiscussionMessage message,
        IReadOnlyCollection<Guid> addedMentionIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// What the caller may do with the discussion settings of one scope, such as an intake desk, or of the channel
    /// type's default when <paramref name="scopeId"/> is null. Null when the channel type has no settings, the scope
    /// does not exist, or the caller cannot see it, which every caller reports as not found.
    /// </summary>
    Task<DiscussionSettingsAccess?> ResolveSettings(Guid? scopeId, CancellationToken cancellationToken);
}

/// <summary>What the caller may do with one scope's discussion settings, as its channel type answers it now.</summary>
/// <param name="CanManage">Whether the caller may change them.</param>
public sealed record DiscussionSettingsAccess(bool CanManage);

/// <summary>
/// What the caller may do in one record's conversation, as its channel type answers it now.
/// </summary>
/// <remarks>Reading replies is implied: a caller who cannot is given no access at all.</remarks>
/// <param name="CanReadInternal">Whether the caller may read internal notes.</param>
/// <param name="CanPost">Whether the caller may post and reply.</param>
/// <param name="CanPostInternal">Whether the caller may post internal notes and reply in their threads.</param>
/// <param name="CanModerate">Whether the caller may lock the conversation and delete anyone's message.</param>
/// <param name="ScopeId">
/// The record whose discussion settings apply to this one, such as an intake request's desk, as the record is now.
/// Null when the channel type has no settings scope, so its default settings apply.
/// </param>
public sealed record DiscussionAccess(bool CanReadInternal, bool CanPost, bool CanPostInternal, bool CanModerate, Guid? ScopeId = null)
{
    /// <summary>Whether the caller may read messages of the given kind.</summary>
    public bool CanRead(DiscussionMessageKind kind) => kind == DiscussionMessageKind.Public || CanReadInternal;

    /// <summary>Whether the caller may write a message of the given kind: post, reply in its thread, or edit one.</summary>
    public bool CanWrite(DiscussionMessageKind kind) =>
        kind == DiscussionMessageKind.InternalNote ? CanPostInternal && CanReadInternal : CanPost;

    /// <summary>The kinds of message the caller may read, for filtering a query.</summary>
    public DiscussionMessageKind[] ReadableKinds() =>
        CanReadInternal ? [DiscussionMessageKind.Public, DiscussionMessageKind.InternalNote] : [DiscussionMessageKind.Public];
}
