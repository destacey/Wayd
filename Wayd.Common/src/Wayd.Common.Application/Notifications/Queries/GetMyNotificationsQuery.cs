using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Following;
using Wayd.Common.Application.Notifications.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Application.Notifications.Queries;

/// <summary>
/// One page of the caller's notification list: one entry per record they were told about and can still read,
/// newest change first.
/// </summary>
/// <remarks>
/// Every notification about one record collapses into one entry, so a busy record does not flood the list;
/// nothing is stored differently, and the entry carries how many it holds and how many are unread.
/// Readability is applied before anything is grouped, counted or paged, through each record's own read rule,
/// so a notification about a record the caller can no longer see is never shown and never counted.
/// <para>
/// Paged by cursor, not offset: an entry moves to the top whenever its record changes, and drops out when the
/// caller loses access, so an offset would repeat or skip entries as the caller reads on. The next page starts
/// strictly after the last entry seen, wherever the list has moved since.
/// </para>
/// <para>
/// Not <see cref="IRequireLinkedEmployee"/>: a caller with no employee has been told nothing, so the answer is
/// an empty page.
/// </para>
/// </remarks>
/// <param name="Paging">The page to read.</param>
/// <param name="UnreadOnly">Keeps only entries with something unread.</param>
public sealed record GetMyNotificationsQuery(CursorRequest Paging, bool UnreadOnly = false)
    : IQuery<CursorPage<NotificationDto>>, ICursorQuery;

/// <summary>Bounds the page size and refuses a cursor the API did not hand out.</summary>
public sealed class GetMyNotificationsQueryValidator : CursorQueryValidator<GetMyNotificationsQuery, NotificationCursor>;

/// <summary>Reads one page of the caller's notification list and describes the records on it.</summary>
public sealed class GetMyNotificationsQueryHandler(
    INotificationsDbContext notificationsDbContext,
    IWaydDbContext waydDbContext,
    IEnumerable<IRecordReadRule> readRules,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetMyNotificationsQuery, CursorPage<NotificationDto>>
{
    private readonly INotificationsDbContext _notificationsDbContext = notificationsDbContext;
    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly IEnumerable<IRecordReadRule> _readRules = readRules;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<CursorPage<NotificationDto>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
        {
            return CursorPage<NotificationDto>.Empty;
        }

        var owners = FollowReadRules.Select(_readRules, null).Value;
        var rows = await NotificationRows.Build(_notificationsDbContext.Notifications, employeeId.Value, owners, cancellationToken);
        if (rows is null)
        {
            return CursorPage<NotificationDto>.Empty;
        }

        var groups = rows.ByRecord();
        if (request.UnreadOnly)
        {
            groups = groups.Where(g => g.UnreadCount > 0);
        }

        if (NotificationCursor.TryDecode(request.Paging.Before, out var before))
        {
            groups = groups.Where(g => g.LatestOn < before.LatestOn
                || (g.LatestOn == before.LatestOn
                    && (string.Compare(g.OwnerType, before.OwnerType) > 0
                        || (g.OwnerType == before.OwnerType && g.OwnerId.CompareTo(before.OwnerId) > 0))));
        }

        // One more than asked for says whether another page follows, without counting the whole list.
        var page = await groups
            .OrderByDescending(g => g.LatestOn)
            .ThenBy(g => g.OwnerType)
            .ThenBy(g => g.OwnerId)
            .Take(request.Paging.PageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = page.Count > request.Paging.PageSize;
        if (hasMore)
        {
            page.RemoveAt(page.Count - 1);
        }

        var items = await Render(employeeId.Value, page, owners, cancellationToken);

        // From the page's rows, not the rendered entries: an entry dropped while rendering still ends the page.
        var last = page.Count > 0 ? page[^1] : null;
        return new CursorPage<NotificationDto>
        {
            Items = items,
            NextCursor = hasMore && last is not null
                ? new NotificationCursor(last.LatestOn, last.OwnerType, last.OwnerId).Encode()
                : null,
        };
    }

    private async Task<List<NotificationDto>> Render(
        Guid employeeId,
        List<NotificationGroupRow> page,
        List<(FollowOwnerDescriptor Descriptor, IRecordReadRule ReadRule)> owners,
        CancellationToken cancellationToken)
    {
        var latest = await Latest(employeeId, page, owners, cancellationToken);
        var summaries = await FollowReadRules.Describe(page.Select(g => (g.OwnerType, g.OwnerId)), owners, cancellationToken);
        var actors = await Actors(latest.Values, cancellationToken);
        var actorHidden = await ActorHidden(page, owners, cancellationToken);

        var items = new List<NotificationDto>(page.Count);
        foreach (var group in page)
        {
            var key = (group.OwnerType, group.OwnerId);

            // A record deleted, or a notification trimmed, between the page query and these reads is dropped
            // rather than shown incomplete.
            if (!summaries.TryGetValue(key, out var summary) || !latest.TryGetValue(key, out var newest))
            {
                continue;
            }

            var actor = newest.ActorEmployeeId is { } actorId && !actorHidden.Contains(key)
                ? actors.GetValueOrDefault(actorId)
                : null;
            items.Add(NotificationDto.Create(newest, group.UnreadCount, group.Count, group.Reasons, actor, summary));
        }

        return items;
    }

    /// <summary>The entries on the page whose record withholds from the caller who made its changes.</summary>
    private static async Task<HashSet<(string OwnerType, Guid OwnerId)>> ActorHidden(
        List<NotificationGroupRow> page,
        List<(FollowOwnerDescriptor Descriptor, IRecordReadRule ReadRule)> owners,
        CancellationToken cancellationToken)
    {
        var hidden = new HashSet<(string OwnerType, Guid OwnerId)>();
        foreach (var (descriptor, readRule) in owners)
        {
            var ownerType = descriptor.Key;
            var ids = page.Where(g => g.OwnerType == ownerType).Select(g => g.OwnerId).Distinct().ToList();
            if (ids.Count == 0)
            {
                continue;
            }

            foreach (var id in await readRule.ActorHiddenOn(ids, cancellationToken))
            {
                hidden.Add((ownerType, id));
            }
        }

        return hidden;
    }

    /// <summary>
    /// The newest notification of each entry on the page: the latest change, and the highest id among
    /// changes recorded at that same instant.
    /// </summary>
    /// <remarks>
    /// Reads only the rows at each entry's newest instant, so a busy record costs one row here rather than
    /// every notification it has had. Read through each owner type's
    /// <see cref="IRecordReadRule.VisibleNotifications"/>, one query per owner type on the page, so the change an
    /// entry leads with is never one the caller may not be told about.
    /// </remarks>
    private async Task<Dictionary<(string OwnerType, Guid OwnerId), Notification>> Latest(
        Guid employeeId,
        List<NotificationGroupRow> page,
        List<(FollowOwnerDescriptor Descriptor, IRecordReadRule ReadRule)> owners,
        CancellationToken cancellationToken)
    {
        var latest = new Dictionary<(string OwnerType, Guid OwnerId), Notification>();

        foreach (var (descriptor, readRule) in owners)
        {
            var ownerType = descriptor.Key;
            var ownerIds = page.Where(g => g.OwnerType == ownerType).Select(g => g.OwnerId).Distinct().ToList();
            if (ownerIds.Count == 0)
            {
                continue;
            }

            var notifications = await readRule.VisibleNotifications(
                _notificationsDbContext.Notifications
                    .Where(n => n.RecipientEmployeeId == employeeId && n.OwnerType == ownerType),
                cancellationToken);

            // The instants are compared in SQL rather than sent as a list, which a converted type cannot be.
            var candidates = await notifications
                .Where(n => ownerIds.Contains(n.OwnerId)
                    && n.OccurredOn == notifications
                        .Where(m => m.OwnerId == n.OwnerId)
                        .Max(m => m.OccurredOn))
                .ToListAsync(cancellationToken);

            foreach (var group in candidates.GroupBy(n => n.OwnerId))
            {
                latest[(ownerType, group.Key)] = group.MaxBy(n => n.Id)!;
            }
        }

        return latest;
    }

    /// <summary>The people who made the page's newest changes, named as they are now.</summary>
    private async Task<Dictionary<Guid, EmployeeNavigationDto>> Actors(
        IEnumerable<Notification> latest,
        CancellationToken cancellationToken)
    {
        var actorIds = latest
            .Where(n => n.ActorEmployeeId.HasValue)
            .Select(n => n.ActorEmployeeId!.Value)
            .Distinct()
            .ToList();
        if (actorIds.Count == 0)
        {
            return [];
        }

        // Unfiltered: someone who has since left still made the change, and is still named for it.
        var employees = await _waydDbContext.Employees
            .IgnoreQueryFilters()
            .Where(e => actorIds.Contains(e.Id))
            .ToListAsync(cancellationToken);

        return employees.ToDictionary(e => e.Id, EmployeeNavigationDto.From);
    }
}
