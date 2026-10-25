using Mapster;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Application.Notifications.Dtos;

/// <summary>
/// One entry in the caller's notification list: everything they were told about one record, collapsed, with
/// the newest change in front.
/// </summary>
/// <remarks>
/// Carries facts for the reader to word, not sentences: what happened, who did it, why the caller was told,
/// and the record as it is now.
/// </remarks>
public sealed record NotificationDto
{
    /// <summary>The record the notifications are about.</summary>
    public required NotifiedRecordDto Record { get; init; }

    /// <summary>The newest notification in the entry; mark it read on its own with this id.</summary>
    public required long LatestNotificationId { get; init; }

    /// <summary>What the newest change was.</summary>
    public required NotificationEvent Event { get; init; }

    /// <summary>The domain event behind the newest change: the id of its entry in the record's activity.</summary>
    public required Guid EventId { get; init; }

    /// <summary>When the newest change happened.</summary>
    public required Instant OccurredOn { get; init; }

    /// <summary>How the newest change was made: a person directly, or the platform reacting for one.</summary>
    public required EventActorKind ActorKind { get; init; }

    /// <summary>Who made the newest change, or null when no employee is linked to the account that did.</summary>
    public EmployeeNavigationDto? Actor { get; init; }

    /// <summary>Every reason the caller was told about this record, across the whole entry.</summary>
    public required IReadOnlyCollection<NotificationReason> Reasons { get; init; }

    /// <summary>How many of the entry's notifications the caller has not read.</summary>
    public required int UnreadCount { get; init; }

    /// <summary>How many notifications the entry collapses.</summary>
    public required int Count { get; init; }

    /// <summary>Whether anything in the entry is unread.</summary>
    public bool IsUnread => UnreadCount > 0;

    /// <summary>An entry from its newest notification, the entry's counts and reasons, and the record as it is now.</summary>
    internal static NotificationDto Create(
        Notification latest,
        int unreadCount,
        int count,
        NotificationReason reasons,
        EmployeeNavigationDto? actor,
        RecordSummary summary) =>
        new()
        {
            Record = NotifiedRecordDto.Create(latest.OwnerType, latest.OwnerId, summary),
            LatestNotificationId = latest.Id,
            Event = latest.Event,
            EventId = latest.EventId,
            OccurredOn = latest.OccurredOn,
            ActorKind = latest.ActorKind,
            Actor = actor,
            Reasons = reasons.ToFlags(),
            UnreadCount = unreadCount,
            Count = count,
        };
}

/// <summary>
/// The record a notification is about, as its owner type describes it now.
/// </summary>
public sealed record NotifiedRecordDto
{
    /// <summary>The record's owner type, such as <c>ppm.project</c>.</summary>
    public required string OwnerType { get; init; }

    /// <summary>The record's id.</summary>
    public required Guid Id { get; init; }

    /// <summary>The record's human-readable key, as its own pages address it.</summary>
    public required string Key { get; init; }

    /// <summary>The record's name or title.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The key of the record this one is addressed under, such as a planning interval objective's planning
    /// interval. Null when the owner type does not nest.
    /// </summary>
    public string? ParentKey { get; init; }

    /// <summary>The name of the parent record. Null when there is none.</summary>
    public string? ParentName { get; init; }

    /// <summary>The record's status as its own pages name it.</summary>
    public required string StatusName { get; init; }

    /// <summary>A record as its owner type describes it.</summary>
    public static NotifiedRecordDto Create(string ownerType, Guid id, RecordSummary summary) =>
        summary.Adapt<NotifiedRecordDto>() with
        {
            OwnerType = ownerType,
            Id = id,
        };
}

/// <summary>
/// How many records the caller has unread notifications about, capped for display.
/// </summary>
public sealed record UnreadNotificationCountDto
{
    /// <summary>The count, never more than <see cref="Cap"/>.</summary>
    public required int Count { get; init; }

    /// <summary>Whether the real count is higher than <see cref="Count"/>, shown as "99+".</summary>
    public required bool IsCapped { get; init; }

    /// <summary>The highest count reported exactly.</summary>
    public const int Cap = 99;
}
