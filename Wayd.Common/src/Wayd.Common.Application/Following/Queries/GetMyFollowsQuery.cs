using Wayd.Common.Application.Records;
using Wayd.Common.Application.Following.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Following;

namespace Wayd.Common.Application.Following.Queries;

/// <summary>
/// The records the caller follows and can still read, grouped by owner type and paged within each group.
/// </summary>
/// <remarks>
/// With <see cref="OwnerType"/> set, returns at most that one group, at the requested page; without it,
/// the requested page of every group. A group with nothing readable in it is left out, so the list never
/// hints at records the caller has lost access to. Fails for an owner type that cannot be followed.
/// <para>
/// Not <see cref="IRequireLinkedEmployee"/>: a caller with no employee follows nothing, so the answer is an
/// empty list.
/// </para>
/// <para>
/// One count and one page query per owner type, each the caller's follows joined to what the owner type
/// says they may read, so the cost grows with the number of owner types rather than the number of
/// follows. Readability is applied before counting: a total that included unreadable follows would say
/// how many records the caller cannot see.
/// </para>
/// </remarks>
public sealed record GetMyFollowsQuery(string? OwnerType = null, int PageNumber = 1, int PageSize = 20)
    : IQuery<Result<List<FollowGroupDto>>>;

/// <summary>Bounds the paging a caller may ask for.</summary>
public sealed class GetMyFollowsQueryValidator : CustomValidator<GetMyFollowsQuery>
{
    /// <summary>The largest page a caller may ask for.</summary>
    public const int MaxPageSize = 100;

    public GetMyFollowsQueryValidator()
    {
        RuleFor(q => q.OwnerType).MaximumLength(FollowOwnerDescriptor.KeyMaxLength);
        RuleFor(q => q.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, MaxPageSize);
    }
}

/// <summary>Reads the caller's readable follows, one count and one page per owner type.</summary>
public sealed class GetMyFollowsQueryHandler(
    INotificationsDbContext notificationsDbContext,
    IEnumerable<IRecordReadRule> readRules,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetMyFollowsQuery, Result<List<FollowGroupDto>>>
{
    private readonly INotificationsDbContext _notificationsDbContext = notificationsDbContext;
    private readonly IEnumerable<IRecordReadRule> _readRules = readRules;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<Result<List<FollowGroupDto>>> Handle(GetMyFollowsQuery request, CancellationToken cancellationToken)
    {
        List<(FollowOwnerDescriptor Descriptor, IRecordReadRule ReadRule)> owners;
        if (request.OwnerType is null)
        {
            owners = [.. FollowOwners.All
                .Select(d => FollowReadRules.Resolve(_readRules, d.Key))
                .Where(r => r.IsSuccess)
                .Select(r => r.Value)];
        }
        else
        {
            var resolved = FollowReadRules.Resolve(_readRules, request.OwnerType);
            if (resolved.IsFailure)
            {
                return Result.Failure<List<FollowGroupDto>>(resolved.Error);
            }

            owners = [resolved.Value];
        }

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
        {
            return new List<FollowGroupDto>();
        }

        var groups = new List<FollowGroupDto>();
        foreach (var (descriptor, readRule) in owners)
        {
            var group = await ReadGroup(descriptor, readRule, employeeId.Value, request.PageNumber, request.PageSize, cancellationToken);
            if (group is not null)
            {
                groups.Add(group);
            }
        }

        return groups;
    }

    private async Task<FollowGroupDto?> ReadGroup(
        FollowOwnerDescriptor descriptor,
        IRecordReadRule readRule,
        Guid employeeId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        // A type the caller cannot read at all contributes nothing, and its follows are never even counted.
        var readable = await readRule.ReadableIds(cancellationToken);
        if (readable is null)
        {
            return null;
        }

        var ownerType = descriptor.Key;
        var follows = _notificationsDbContext.Follows
            .Where(f => f.EmployeeId == employeeId && f.OwnerType == ownerType && readable.Contains(f.OwnerId));

        var totalCount = await follows.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return null;
        }

        var page = await follows
            .OrderByDescending(f => f.FollowedOn)
            .ThenBy(f => f.OwnerId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new { f.OwnerId, f.FollowedOn })
            .ToListAsync(cancellationToken);

        var summaries = page.Count > 0
            ? await readRule.Describe([.. page.Select(f => f.OwnerId)], cancellationToken)
            : new Dictionary<Guid, RecordSummary>();

        // A record deleted between the page query and Describe is dropped rather than shown unnamed.
        var items = page
            .Where(f => summaries.ContainsKey(f.OwnerId))
            .Select(f =>
            {
                var summary = summaries[f.OwnerId];
                return new FollowedRecordDto
                {
                    OwnerType = ownerType,
                    Id = f.OwnerId,
                    Key = summary.Key,
                    Name = summary.Name,
                    ParentKey = summary.ParentKey,
                    ParentName = summary.ParentName,
                    FollowedOn = f.FollowedOn,
                };
            })
            .ToList();

        return new FollowGroupDto
        {
            OwnerType = ownerType,
            DisplayName = descriptor.DisplayName,
            Follows = new PagedResponse<FollowedRecordDto>(items, totalCount, pageNumber, pageSize),
        };
    }
}
