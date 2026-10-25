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
/// Not <see cref="IRequireLinkedEmployee"/>: a caller with no employee has been told nothing, so the answer is
/// an empty page.
/// </para>
/// </remarks>
public sealed record GetMyNotificationsQuery(bool UnreadOnly = false, int PageNumber = 1, int PageSize = 25)
    : IQuery<PagedResponse<NotificationDto>>;

/// <summary>Bounds the paging a caller may ask for.</summary>
public sealed class GetMyNotificationsQueryValidator : CustomValidator<GetMyNotificationsQuery>
{
    /// <summary>The largest page a caller may ask for.</summary>
    public const int MaxPageSize = 100;

    public GetMyNotificationsQueryValidator()
    {
        RuleFor(q => q.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, MaxPageSize);
    }
}

/// <summary>Reads one page of the caller's notification list and describes the records on it.</summary>
public sealed class GetMyNotificationsQueryHandler(
    INotificationsDbContext notificationsDbContext,
    IWaydDbContext waydDbContext,
    IEnumerable<IRecordReadRule> readRules,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetMyNotificationsQuery, PagedResponse<NotificationDto>>
{
    private readonly INotificationsDbContext _notificationsDbContext = notificationsDbContext;
    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly IEnumerable<IRecordReadRule> _readRules = readRules;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<PagedResponse<NotificationDto>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        var empty = new PagedResponse<NotificationDto>([], 0, request.PageNumber, request.PageSize);

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
        {
            return empty;
        }

        var owners = FollowReadRules.Select(_readRules, null).Value;
        var rows = await NotificationRows.Build(_notificationsDbContext.Notifications, employeeId.Value, owners, cancellationToken);
        if (rows is null)
        {
            return empty;
        }

        var groups = rows.ByRecord();
        if (request.UnreadOnly)
        {
            groups = groups.Where(g => g.UnreadCount > 0);
        }

        var totalCount = await groups.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return empty;
        }

        var page = await groups
            .OrderByDescending(g => g.LatestOn)
            .ThenByDescending(g => g.LatestId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = await Render(employeeId.Value, page, owners, cancellationToken);

        return new PagedResponse<NotificationDto>(items, totalCount, request.PageNumber, request.PageSize);
    }

    private async Task<List<NotificationDto>> Render(
        Guid employeeId,
        List<NotificationGroupRow> page,
        List<(FollowOwnerDescriptor Descriptor, IRecordReadRule ReadRule)> owners,
        CancellationToken cancellationToken)
    {
        var latest = await Latest(employeeId, page, cancellationToken);
        var summaries = await FollowReadRules.Describe(page.Select(g => (g.OwnerType, g.OwnerId)), owners, cancellationToken);
        var actors = await Actors(latest.Values, cancellationToken);

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

            var actor = newest.ActorEmployeeId is { } actorId ? actors.GetValueOrDefault(actorId) : null;
            items.Add(NotificationDto.Create(newest, group.UnreadCount, group.Count, group.Reasons, actor, summary));
        }

        return items;
    }

    /// <summary>
    /// The newest notification of each entry on the page: the latest change, and the highest id among
    /// changes recorded at that same instant.
    /// </summary>
    /// <remarks>
    /// Reads only the rows at each entry's newest instant, so a busy record costs one row here rather than
    /// every notification it has had.
    /// </remarks>
    private async Task<Dictionary<(string OwnerType, Guid OwnerId), Notification>> Latest(
        Guid employeeId,
        List<NotificationGroupRow> page,
        CancellationToken cancellationToken)
    {
        var ownerIds = page.Select(g => g.OwnerId).Distinct().ToList();
        var pageKeys = page.Select(g => (g.OwnerType, g.OwnerId)).ToHashSet();
        var notifications = _notificationsDbContext.Notifications.Where(n => n.RecipientEmployeeId == employeeId);

        // Owner ids can repeat across owner types, so the read narrows by id and the match is made on the full
        // key. The instants are compared in SQL rather than sent as a list, which a converted type cannot be.
        var candidates = await notifications
            .Where(n => ownerIds.Contains(n.OwnerId)
                && n.OccurredOn == notifications
                    .Where(m => m.OwnerType == n.OwnerType && m.OwnerId == n.OwnerId)
                    .Max(m => m.OccurredOn))
            .ToListAsync(cancellationToken);

        return candidates
            .Where(n => pageKeys.Contains((n.OwnerType, n.OwnerId)))
            .GroupBy(n => (n.OwnerType, n.OwnerId))
            .ToDictionary(g => g.Key, g => g.MaxBy(n => n.Id)!);
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
