using Wayd.Common.Application.Following;
using Wayd.Common.Application.Notifications.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.Records;

namespace Wayd.Common.Application.Notifications.Queries;

/// <summary>
/// How many records the caller has unread notifications about and can still read, capped at
/// <see cref="UnreadNotificationCountDto.Cap"/>.
/// </summary>
/// <remarks>
/// Counts entries, as the list shows them, not notifications: five unread changes to one project are one
/// thing to look at. Capped rather than counted exactly, because the bell asks on every page load and only
/// needs to know whether it is "99+". Not <see cref="IRequireLinkedEmployee"/>: a caller with no employee has
/// nothing unread.
/// </remarks>
public sealed record GetMyUnreadNotificationCountQuery : IQuery<UnreadNotificationCountDto>;

/// <summary>Counts the caller's unread entries up to the cap.</summary>
public sealed class GetMyUnreadNotificationCountQueryHandler(
    INotificationsDbContext notificationsDbContext,
    IEnumerable<IRecordReadRule> readRules,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetMyUnreadNotificationCountQuery, UnreadNotificationCountDto>
{
    private readonly INotificationsDbContext _notificationsDbContext = notificationsDbContext;
    private readonly IEnumerable<IRecordReadRule> _readRules = readRules;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<UnreadNotificationCountDto> Handle(GetMyUnreadNotificationCountQuery request, CancellationToken cancellationToken)
    {
        var none = new UnreadNotificationCountDto { Count = 0, IsCapped = false };

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
        {
            return none;
        }

        var owners = FollowReadRules.Select(_readRules, null).Value;
        var rows = await NotificationRows.Build(_notificationsDbContext.Notifications, employeeId.Value, owners, cancellationToken);
        if (rows is null)
        {
            return none;
        }

        // One past the cap, so the database stops as soon as it knows the answer is "more".
        var count = await rows
            .Where(r => r.IsUnread)
            .Select(r => new { r.OwnerType, r.OwnerId })
            .Distinct()
            .Take(UnreadNotificationCountDto.Cap + 1)
            .CountAsync(cancellationToken);

        return count > UnreadNotificationCountDto.Cap
            ? new UnreadNotificationCountDto { Count = UnreadNotificationCountDto.Cap, IsCapped = true }
            : new UnreadNotificationCountDto { Count = count, IsCapped = false };
    }
}
