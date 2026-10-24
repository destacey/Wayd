using Wayd.Common.Application.Following.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.Records;

namespace Wayd.Common.Application.Following.Queries;

/// <summary>
/// How many records the caller follows and can still read: in all, per owner type, and per "needs a look" view.
/// </summary>
/// <remarks>
/// Counted after readability, like the list: an owner type the caller cannot read is left out altogether
/// rather than shown with a count. Not <see cref="IRequireLinkedEmployee"/>: a caller with no employee follows
/// nothing, so every count is zero.
/// </remarks>
public sealed record GetMyFollowCountsQuery : IQuery<FollowCountsDto>;

/// <summary>Counts the caller's readable follows.</summary>
public sealed class GetMyFollowCountsQueryHandler(
    INotificationsDbContext notificationsDbContext,
    IActivityLogDbContext activityLogDbContext,
    IEnumerable<IRecordReadRule> readRules,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetMyFollowCountsQuery, FollowCountsDto>
{
    private readonly INotificationsDbContext _notificationsDbContext = notificationsDbContext;
    private readonly IActivityLogDbContext _activityLogDbContext = activityLogDbContext;
    private readonly IEnumerable<IRecordReadRule> _readRules = readRules;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<FollowCountsDto> Handle(GetMyFollowCountsQuery request, CancellationToken cancellationToken)
    {
        var owners = FollowReadRules.Select(_readRules, null).Value;
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);

        var types = new List<FollowTypeCountDto>();
        IQueryable<FollowedRow>? all = null;

        foreach (var owner in owners)
        {
            if (employeeId is null)
            {
                // Still list what the caller can read, so the rail shows the kinds they could follow.
                if (await owner.ReadRule.Readable(null, cancellationToken) is not null)
                {
                    types.Add(new FollowTypeCountDto { OwnerType = owner.Descriptor.Key, DisplayName = owner.Descriptor.DisplayName, Count = 0 });
                }

                continue;
            }

            var rows = await FollowedRows.Build(
                _notificationsDbContext.Follows,
                _activityLogDbContext.ActivityLogs,
                employeeId.Value,
                [owner],
                null,
                cancellationToken);
            if (rows is null)
            {
                continue;
            }

            types.Add(new FollowTypeCountDto
            {
                OwnerType = owner.Descriptor.Key,
                DisplayName = owner.Descriptor.DisplayName,
                Count = await rows.CountAsync(cancellationToken),
            });
            all = all is null ? rows : all.Concat(rows);
        }

        var now = _dateTimeProvider.Now;
        var views = new List<FollowViewCountDto>();
        foreach (var view in Enum.GetValues<FollowView>())
        {
            views.Add(new FollowViewCountDto
            {
                View = view,
                Count = all is null ? 0 : await all.In(view, now).CountAsync(cancellationToken),
            });
        }

        return new FollowCountsDto
        {
            Total = types.Sum(t => t.Count),
            Types = types,
            Views = views,
        };
    }
}
