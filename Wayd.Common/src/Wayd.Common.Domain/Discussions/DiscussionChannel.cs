using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Events.Discussions;

namespace Wayd.Common.Domain.Discussions;

/// <summary>
/// One record's conversation: the container its messages belong to, and the state that applies to all of them.
/// </summary>
/// <remarks>
/// <strong>It holds nothing about its record.</strong> Who may read or post, and which scope's settings apply, are
/// worked out from the record whenever they are needed, through the module's channel type, so a rerouted request or
/// a moved project never leaves a stale channel. <see cref="OwnerId"/> is deliberately not a foreign key: it points
/// at a different table per channel type, as a follow's owner does.
/// <para>
/// Opened by the first message posted on a record, or by a moderator locking a record nobody has posted on yet.
/// Its messages are a separate aggregate and are never loaded through it, since a conversation can run to thousands
/// of them.
/// </para>
/// </remarks>
public sealed class DiscussionChannel : BaseEntity
{
    private DiscussionChannel() { }

    private DiscussionChannel(string channelType, Guid ownerId, Instant createdOn)
    {
        ChannelType = channelType;
        OwnerId = ownerId;
        CreatedOn = createdOn;
    }

    /// <summary>
    /// Opens the conversation on a record, failing when the channel type is not registered.
    /// </summary>
    /// <remarks>
    /// Says nothing about whether the actor may post on the record. That is the channel type's answer, asked before
    /// a channel is opened.
    /// </remarks>
    public static Result<DiscussionChannel> OpenForRecord(string channelType, Guid ownerId, EventActor actor, Instant timestamp)
    {
        var descriptor = DiscussionChannelTypes.Resolve(channelType);
        if (descriptor.IsFailure)
        {
            return Result.Failure<DiscussionChannel>(descriptor.Error);
        }

        Guard.Against.Default(ownerId, nameof(ownerId));
        Guard.Against.Null(actor, nameof(actor));

        // The registered key rather than the caller's spelling, so a channel is stored under one casing.
        var channel = new DiscussionChannel(descriptor.Value.Key, ownerId, timestamp);
        channel.AddDomainEvent(new DiscussionChannelOpenedEvent(channel.Id, channel.ChannelType, channel.OwnerId, actor, timestamp));

        return channel;
    }

    /// <summary>The registered channel type: what kind of record the conversation is about.</summary>
    public string ChannelType { get; private init; } = default!;

    /// <summary>
    /// The record the conversation is about, unique only within <see cref="ChannelType"/>. Null is reserved for a
    /// conversation with no record; every channel opened today has one.
    /// </summary>
    public Guid? OwnerId { get; private init; }

    /// <summary>When the conversation was opened: by its first message, or by a moderator locking it first.</summary>
    public Instant CreatedOn { get; private init; }

    /// <summary>When the conversation was locked, or null while it is open.</summary>
    public Instant? LockedOn { get; private set; }

    /// <summary>The employee who locked the conversation, or null while it is open.</summary>
    public Guid? LockedById { get; private set; }

    /// <summary>Whether the conversation is locked: nothing new can be posted, replied or edited.</summary>
    public bool IsLocked => LockedOn is not null;

    /// <summary>
    /// Locks the conversation. Whether the employee may moderate it is the channel type's answer, asked first.
    /// </summary>
    public Result Lock(Guid lockedById, EventActor actor, Instant timestamp)
    {
        Guard.Against.Default(lockedById, nameof(lockedById));

        if (IsLocked)
        {
            return Result.Failure(DiscussionErrors.AlreadyLocked);
        }

        LockedOn = timestamp;
        LockedById = lockedById;
        AddDomainEvent(new DiscussionChannelLockedEvent(Id, ChannelType, OwnerId, lockedById, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Unlocks the conversation. Whether the actor may moderate it is the channel type's answer, asked first.
    /// </summary>
    public Result Unlock(EventActor actor, Instant timestamp)
    {
        if (!IsLocked)
        {
            return Result.Failure(DiscussionErrors.NotLocked);
        }

        LockedOn = null;
        LockedById = null;
        AddDomainEvent(new DiscussionChannelUnlockedEvent(Id, ChannelType, OwnerId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Records that the conversation is being removed, with every message in it, because its record is being deleted.
    /// The caller then removes the channel; the database removes its messages, their revisions and mentions.
    /// </summary>
    /// <remarks>
    /// One event for the conversation rather than one per message: the messages go because the record went, not
    /// because anyone deleted them, and they leave no tombstones because there is no conversation left to show them in.
    /// </remarks>
    public void Delete(EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(actor, nameof(actor));
        AddDomainEvent(new DiscussionChannelDeletedEvent(Id, ChannelType, OwnerId, actor, timestamp));
    }
}
