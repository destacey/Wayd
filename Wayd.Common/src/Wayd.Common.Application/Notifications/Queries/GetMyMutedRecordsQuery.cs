using Wayd.Common.Application.Following;
using Wayd.Common.Application.Notifications.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Application.Notifications.Queries;

/// <summary>
/// One page of the records the caller has muted and can still read, most recently muted first.
/// </summary>
/// <remarks>
/// Readability is applied before anything is counted or paged, through each record's own read rule, so the
/// list never names or counts a record the caller cannot see. A mute of an unreadable record is left out
/// rather than shown, though it still applies; it can be cleared through the record's own unmute endpoint.
/// <para>
/// Paged by number: the order is fixed by when each mute was set, so it does not move while being read.
/// Not <see cref="IRequireLinkedEmployee"/>: a caller with no employee has muted nothing.
/// </para>
/// </remarks>
/// <param name="Paging">The page to read.</param>
public sealed record GetMyMutedRecordsQuery(PageRequest Paging) : IQuery<PagedResponse<MutedRecordDto>>, IPagedQuery;

/// <summary>Bounds the paging a caller may ask for.</summary>
public sealed class GetMyMutedRecordsQueryValidator : PagedQueryValidator<GetMyMutedRecordsQuery>;

/// <summary>Reads one page of the caller's readable record mutes and describes the records on it.</summary>
public sealed class GetMyMutedRecordsQueryHandler(
    INotificationsDbContext notificationsDbContext,
    IEnumerable<IRecordReadRule> readRules,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetMyMutedRecordsQuery, PagedResponse<MutedRecordDto>>
{
    private readonly INotificationsDbContext _notificationsDbContext = notificationsDbContext;
    private readonly IEnumerable<IRecordReadRule> _readRules = readRules;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<PagedResponse<MutedRecordDto>> Handle(GetMyMutedRecordsQuery request, CancellationToken cancellationToken)
    {
        var empty = new PagedResponse<MutedRecordDto>([], 0, request.Paging.Page, request.Paging.PageSize);

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
        {
            return empty;
        }

        var owners = FollowReadRules.Select(_readRules, null).Value;
        var rows = await Build(employeeId.Value, owners, cancellationToken);
        if (rows is null)
        {
            return empty;
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return empty;
        }

        var page = await rows
            .OrderByDescending(r => r.MutedOn)
            .ThenBy(r => r.OwnerType)
            .ThenBy(r => r.OwnerId)
            .Skip(request.Paging.Skip)
            .Take(request.Paging.PageSize)
            .ToListAsync(cancellationToken);

        var summaries = await FollowReadRules.Describe(page.Select(r => (r.OwnerType, r.OwnerId)), owners, cancellationToken);

        // A record deleted between the page query and Describe is dropped rather than shown unnamed.
        var items = page
            .Where(r => summaries.ContainsKey((r.OwnerType, r.OwnerId)))
            .Select(r => MutedRecordDto.Create(r.OwnerType, r.OwnerId, r.IsFollowing, r.MutedOn, summaries[(r.OwnerType, r.OwnerId)]))
            .ToList();

        return new PagedResponse<MutedRecordDto>(items, totalCount, request.Paging.Page, request.Paging.PageSize);
    }

    /// <summary>
    /// The caller's mutes of records they can still read, across owner types as one query, or null when they
    /// can read no record of any of them.
    /// </summary>
    private async Task<IQueryable<MutedRecordRow>?> Build(
        Guid employeeId,
        IEnumerable<(FollowOwnerDescriptor Descriptor, IRecordReadRule ReadRule)> owners,
        CancellationToken cancellationToken)
    {
        IQueryable<RecordMute> mutes = _notificationsDbContext.RecordMutes;
        IQueryable<Follow> follows = _notificationsDbContext.Follows;
        IQueryable<MutedRecordRow>? all = null;

        foreach (var (descriptor, readRule) in owners)
        {
            var readable = await readRule.Readable(null, cancellationToken);
            if (readable is null)
            {
                continue;
            }

            var ownerType = descriptor.Key;
            var rows =
                from m in mutes
                where m.EmployeeId == employeeId && m.OwnerType == ownerType
                join r in readable on m.OwnerId equals r.Id
                select new MutedRecordRow
                {
                    OwnerType = m.OwnerType,
                    OwnerId = m.OwnerId,
                    MutedOn = m.MutedOn,
                    IsFollowing = follows.Any(f => f.OwnerType == m.OwnerType && f.OwnerId == m.OwnerId && f.EmployeeId == employeeId),
                };

            all = all is null ? rows : all.Concat(rows);
        }

        return all;
    }

    /// <summary>One of the caller's mutes of a readable record, in a shape every owner type produces alike.</summary>
    private sealed class MutedRecordRow
    {
        /// <summary>The registered owner type of the muted record.</summary>
        public string OwnerType { get; init; } = default!;

        /// <summary>The muted record's id.</summary>
        public Guid OwnerId { get; init; }

        /// <summary>When the caller muted the record.</summary>
        public Instant MutedOn { get; init; }

        /// <summary>Whether the caller also follows the record.</summary>
        public bool IsFollowing { get; init; }
    }
}
