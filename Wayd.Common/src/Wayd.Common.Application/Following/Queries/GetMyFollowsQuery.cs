using Wayd.Common.Application.Following.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Following;

namespace Wayd.Common.Application.Following.Queries;

/// <summary>
/// One page of the records the caller follows and can still read, across owner types or for one, optionally
/// narrowed to a "needs a look" view and a search.
/// </summary>
/// <remarks>
/// Readability is applied before anything is counted or paged: a total that included unreadable follows would
/// say how many records the caller cannot see. The owner types are combined in one query, so a page of
/// "everything" is filtered, sorted and paged by the database. Fails for an owner type that cannot be followed.
/// <para>
/// Not <see cref="IRequireLinkedEmployee"/>: a caller with no employee follows nothing, so the answer is an
/// empty page.
/// </para>
/// </remarks>
/// <param name="Paging">The page to read.</param>
/// <param name="OwnerType">Keeps only follows of this owner type; null for every type.</param>
/// <param name="View">Keeps only follows in this view; null for all.</param>
/// <param name="Search">Matched against the record's name, or its key exactly; blank for no search.</param>
/// <param name="Sort">What the page is ordered by.</param>
/// <param name="Direction">Which way it is ordered.</param>
public sealed record GetMyFollowsQuery(
    PageRequest Paging,
    string? OwnerType = null,
    FollowView? View = null,
    string? Search = null,
    FollowSort Sort = FollowSort.LastChanged,
    SortDirection Direction = SortDirection.Descending)
    : IQuery<Result<PagedResponse<FollowedRecordDto>>>, IPagedQuery;

/// <summary>Bounds the paging and search a caller may ask for.</summary>
public sealed class GetMyFollowsQueryValidator : PagedQueryValidator<GetMyFollowsQuery>
{
    /// <summary>The longest search a caller may send.</summary>
    public const int MaxSearchLength = 128;

    public GetMyFollowsQueryValidator()
    {
        RuleFor(q => q.OwnerType).MaximumLength(FollowOwnerDescriptor.KeyMaxLength);
        RuleFor(q => q.View).IsInEnum();
        RuleFor(q => q.Sort).IsInEnum();
        RuleFor(q => q.Direction).IsInEnum();
        RuleFor(q => q.Search).MaximumLength(MaxSearchLength);
    }
}

/// <summary>Reads one page of the caller's readable follows and describes the records on it.</summary>
public sealed class GetMyFollowsQueryHandler(
    INotificationsDbContext notificationsDbContext,
    IActivityLogDbContext activityLogDbContext,
    IEnumerable<IRecordReadRule> readRules,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetMyFollowsQuery, Result<PagedResponse<FollowedRecordDto>>>
{
    private readonly INotificationsDbContext _notificationsDbContext = notificationsDbContext;
    private readonly IActivityLogDbContext _activityLogDbContext = activityLogDbContext;
    private readonly IEnumerable<IRecordReadRule> _readRules = readRules;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<PagedResponse<FollowedRecordDto>>> Handle(GetMyFollowsQuery request, CancellationToken cancellationToken)
    {
        var owners = FollowReadRules.Select(_readRules, request.OwnerType);
        if (owners.IsFailure)
        {
            return Result.Failure<PagedResponse<FollowedRecordDto>>(owners.Error);
        }

        var empty = new PagedResponse<FollowedRecordDto>([], 0, request.Paging.Page, request.Paging.PageSize);

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
        {
            return empty;
        }

        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var rows = await FollowedRows.Build(
            _notificationsDbContext.Follows,
            _activityLogDbContext.ActivityLogs,
            employeeId.Value,
            owners.Value,
            search,
            cancellationToken);
        if (rows is null)
        {
            return empty;
        }

        var filtered = rows.In(request.View, _dateTimeProvider.Now);

        var totalCount = await filtered.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return empty;
        }

        // Records with no activity count as the least recently changed: last when descending, first when
        // ascending. The owner id breaks ties the same way in both directions, so paging stays stable.
        var descending = request.Direction == SortDirection.Descending;
        var ordered = (request.Sort, descending) switch
        {
            (FollowSort.Followed, true) => filtered.OrderByDescending(r => r.FollowedOn),
            (FollowSort.Followed, false) => filtered.OrderBy(r => r.FollowedOn),
            (_, true) => filtered
                .OrderByDescending(r => r.LastChangedOn.HasValue)
                .ThenByDescending(r => r.LastChangedOn)
                .ThenByDescending(r => r.FollowedOn),
            _ => filtered
                .OrderBy(r => r.LastChangedOn.HasValue)
                .ThenBy(r => r.LastChangedOn)
                .ThenBy(r => r.FollowedOn),
        };

        var page = await ordered
            .ThenBy(r => r.OwnerId)
            .Skip(request.Paging.Skip)
            .Take(request.Paging.PageSize)
            .ToListAsync(cancellationToken);

        var summaries = await FollowReadRules.Describe(page.Select(r => (r.OwnerType, r.OwnerId)), owners.Value, cancellationToken);

        // A record deleted between the page query and Describe is dropped rather than shown unnamed.
        var items = page
            .Where(r => summaries.ContainsKey((r.OwnerType, r.OwnerId)))
            .Select(r => FollowedRecordDto.Create(r.OwnerType, r.OwnerId, r.FollowedOn, r.LastChangedOn, summaries[(r.OwnerType, r.OwnerId)]))
            .ToList();

        return new PagedResponse<FollowedRecordDto>(items, totalCount, request.Paging.Page, request.Paging.PageSize);
    }
}
