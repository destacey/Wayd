using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Application.Notifications;

/// <summary>
/// One of the caller's notifications about a record they can still read.
/// </summary>
/// <remarks>
/// Only the notification's own columns, so every owner type produces the same shape and the types combine
/// with <c>UNION ALL</c>.
/// </remarks>
internal sealed class NotificationRow
{
    /// <summary>The notification's id.</summary>
    public long Id { get; init; }

    /// <summary>The registered owner type of the record.</summary>
    public string OwnerType { get; init; } = default!;

    /// <summary>The record's id.</summary>
    public Guid OwnerId { get; init; }

    /// <summary>When the change happened.</summary>
    public Instant OccurredOn { get; init; }

    /// <summary>Whether the caller has not read it yet.</summary>
    public bool IsUnread { get; init; }

    /// <summary>Why the caller was told.</summary>
    public NotificationReason Reasons { get; init; }
}

/// <summary>
/// The caller's notifications about one record, collapsed into the single entry a list shows.
/// </summary>
internal sealed class NotificationGroupRow
{
    /// <summary>The registered owner type of the record.</summary>
    public string OwnerType { get; init; } = default!;

    /// <summary>The record's id.</summary>
    public Guid OwnerId { get; init; }

    /// <summary>When the newest change in the entry happened.</summary>
    public Instant LatestOn { get; init; }

    /// <summary>How many of the entry's notifications are unread.</summary>
    public int UnreadCount { get; init; }

    /// <summary>How many notifications the entry collapses.</summary>
    public int Count { get; init; }

    /// <summary>How many of the entry's notifications carry <see cref="NotificationReason.Following"/>.</summary>
    public int FollowingCount { get; init; }

    /// <summary>How many carry <see cref="NotificationReason.Requester"/>.</summary>
    public int RequesterCount { get; init; }

    /// <summary>How many carry <see cref="NotificationReason.Participant"/>.</summary>
    public int ParticipantCount { get; init; }

    /// <summary>How many carry <see cref="NotificationReason.Assigned"/>.</summary>
    public int AssignedCount { get; init; }

    /// <summary>How many carry <see cref="NotificationReason.Mentioned"/>.</summary>
    public int MentionedCount { get; init; }

    /// <summary>How many carry <see cref="NotificationReason.Desk"/>.</summary>
    public int DeskCount { get; init; }

    /// <summary>How many carry <see cref="NotificationReason.Role"/>.</summary>
    public int RoleCount { get; init; }

    /// <summary>Every reason carried by any of the entry's notifications.</summary>
    public NotificationReason Reasons =>
        (FollowingCount > 0 ? NotificationReason.Following : 0)
        | (RequesterCount > 0 ? NotificationReason.Requester : 0)
        | (ParticipantCount > 0 ? NotificationReason.Participant : 0)
        | (AssignedCount > 0 ? NotificationReason.Assigned : 0)
        | (MentionedCount > 0 ? NotificationReason.Mentioned : 0)
        | (DeskCount > 0 ? NotificationReason.Desk : 0)
        | (RoleCount > 0 ? NotificationReason.Role : 0);
}

/// <summary>
/// Builds the caller's notifications about records they can still read, across owner types, as one query.
/// </summary>
internal static class NotificationRows
{
    /// <summary>
    /// The caller's notifications about records they can still read, or <c>null</c> when they can read no
    /// record of any registered owner type.
    /// </summary>
    /// <remarks>
    /// Each owner type contributes its notifications joined to the records its read rule admits, so a
    /// notification about a record that was deleted, or that the caller can no longer see, never appears —
    /// not in a page, not in a count. Being notified is not evidence of access.
    /// </remarks>
    public static async Task<IQueryable<NotificationRow>?> Build(
        IQueryable<Notification> notifications,
        Guid recipientEmployeeId,
        IEnumerable<(FollowOwnerDescriptor Descriptor, IRecordReadRule ReadRule)> owners,
        CancellationToken cancellationToken)
    {
        IQueryable<NotificationRow>? all = null;

        foreach (var (descriptor, readRule) in owners)
        {
            var readable = await readRule.Readable(null, cancellationToken);
            if (readable is null)
            {
                continue;
            }

            var ownerType = descriptor.Key;
            var rows =
                from n in notifications
                where n.RecipientEmployeeId == recipientEmployeeId && n.OwnerType == ownerType
                join r in readable on n.OwnerId equals r.Id
                select new NotificationRow
                {
                    Id = n.Id,
                    OwnerType = n.OwnerType,
                    OwnerId = n.OwnerId,
                    OccurredOn = n.OccurredOn,
                    IsUnread = n.ReadOn == null,
                    Reasons = n.Reasons,
                };

            all = all is null ? rows : all.Concat(rows);
        }

        return all;
    }

    /// <summary>Collapses the rows into one entry per record.</summary>
    /// <remarks>
    /// The reasons are counted per flag because SQL has no bitwise-OR aggregate; a set flag counts at least once.
    /// </remarks>
    public static IQueryable<NotificationGroupRow> ByRecord(this IQueryable<NotificationRow> rows) =>
        rows
            .GroupBy(r => new { r.OwnerType, r.OwnerId })
            .Select(g => new NotificationGroupRow
            {
                OwnerType = g.Key.OwnerType,
                OwnerId = g.Key.OwnerId,
                LatestOn = g.Max(r => r.OccurredOn),
                UnreadCount = g.Count(r => r.IsUnread),
                Count = g.Count(),
                FollowingCount = g.Count(r => (r.Reasons & NotificationReason.Following) != 0),
                RequesterCount = g.Count(r => (r.Reasons & NotificationReason.Requester) != 0),
                ParticipantCount = g.Count(r => (r.Reasons & NotificationReason.Participant) != 0),
                AssignedCount = g.Count(r => (r.Reasons & NotificationReason.Assigned) != 0),
                MentionedCount = g.Count(r => (r.Reasons & NotificationReason.Mentioned) != 0),
                DeskCount = g.Count(r => (r.Reasons & NotificationReason.Desk) != 0),
                RoleCount = g.Count(r => (r.Reasons & NotificationReason.Role) != 0),
            });
}
