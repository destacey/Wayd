using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Following;

namespace Wayd.Common.Domain.Notifications;

/// <summary>
/// One person told about one change to one record, and why.
/// </summary>
/// <remarks>
/// <strong>Facts, never prose.</strong> It records which record changed, what happened, who did it, when,
/// and why this person was told. The wording is produced when the list is read, from the record as it is
/// then, so a renamed record or status leaves nothing stale behind.
/// <para>
/// <strong>It is not evidence of access.</strong> A notification is written without asking whether its
/// recipient can read the record, and is shown only while they can, through the record's own read rule.
/// </para>
/// <para>
/// One per person per event: <see cref="EventId"/> and <see cref="RecipientEmployeeId"/> are unique together,
/// which is also what makes a redelivered event write nothing twice. Not an audit trail: the activity log
/// holds what happened, and old notifications are trimmed without anything being lost. Raises no events.
/// </para>
/// </remarks>
public sealed class Notification
{
    private Notification() { }

    private Notification(
        Guid recipientEmployeeId,
        string ownerType,
        Guid ownerId,
        Guid eventId,
        NotificationEvent @event,
        EventActorKind actorKind,
        Guid? actorEmployeeId,
        Instant occurredOn,
        NotificationReason reasons)
    {
        RecipientEmployeeId = recipientEmployeeId;
        OwnerType = ownerType;
        OwnerId = ownerId;
        EventId = eventId;
        Event = @event;
        ActorKind = actorKind;
        ActorEmployeeId = actorEmployeeId;
        OccurredOn = occurredOn;
        Reasons = reasons;
    }

    /// <summary>
    /// Creates an unread notification, failing when the owner type is not registered or no reason is given.
    /// </summary>
    /// <remarks>
    /// Says nothing about whether the recipient may read the record, or whether they caused the change:
    /// both are the writer's concern.
    /// </remarks>
    public static Result<Notification> Create(
        Guid recipientEmployeeId,
        string ownerType,
        Guid ownerId,
        Guid eventId,
        NotificationEvent @event,
        EventActorKind actorKind,
        Guid? actorEmployeeId,
        Instant occurredOn,
        NotificationReason reasons)
    {
        var descriptor = FollowOwners.Resolve(ownerType);
        if (descriptor.IsFailure)
        {
            return Result.Failure<Notification>(descriptor.Error);
        }

        if (!NotificationReasons.IsValid(reasons))
        {
            return Result.Failure<Notification>("A notification needs at least one defined reason.");
        }

        Guard.Against.Default(recipientEmployeeId, nameof(recipientEmployeeId));
        Guard.Against.Default(ownerId, nameof(ownerId));
        Guard.Against.Default(eventId, nameof(eventId));
        Guard.Against.EnumOutOfRange(@event, nameof(@event));
        Guard.Against.EnumOutOfRange(actorKind, nameof(actorKind));

        return new Notification(
            recipientEmployeeId,
            descriptor.Value.Key,
            ownerId,
            eventId,
            @event,
            actorKind,
            actorEmployeeId,
            occurredOn,
            reasons);
    }

    /// <summary>The notification's id, assigned by the database in the order notifications are written.</summary>
    public long Id { get; private init; }

    /// <summary>The employee the notification is for.</summary>
    public Guid RecipientEmployeeId { get; private init; }

    /// <summary>The registered owner type of the record that changed.</summary>
    public string OwnerType { get; private init; } = default!;

    /// <summary>The id of the record that changed, unique only within <see cref="OwnerType"/>.</summary>
    public Guid OwnerId { get; private init; }

    /// <summary>
    /// The domain event that caused the notification: the key of its activity log entry, which holds the
    /// full change.
    /// </summary>
    public Guid EventId { get; private init; }

    /// <summary>What happened to the record.</summary>
    public NotificationEvent Event { get; private init; }

    /// <summary>The mechanism that made the change: a person directly, or the platform reacting for one.</summary>
    public EventActorKind ActorKind { get; private init; }

    /// <summary>The employee who made the change, or null when no employee is linked to the account that did.</summary>
    public Guid? ActorEmployeeId { get; private init; }

    /// <summary>When the change happened, as the event recorded it.</summary>
    public Instant OccurredOn { get; private init; }

    /// <summary>Every reason the recipient was told. Never empty.</summary>
    public NotificationReason Reasons { get; private init; }

    /// <summary>When the recipient marked it read, or null while it is unread.</summary>
    public Instant? ReadOn { get; private init; }
}
