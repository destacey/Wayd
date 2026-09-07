using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;

namespace Wayd.Organization.Application.Teams.Queries;

/// <summary>
/// Retrieves activity history for a team, newest first.
/// </summary>
public sealed record GetTeamActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>
{
    public GetTeamActivitiesQuery(IdOrKey idOrKey, int page = 1, int pageSize = 50)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Team>();
        Page = page;
        PageSize = pageSize;
    }

    public Expression<Func<Team, bool>> IdOrKeyFilter { get; }
    public int Page { get; }
    public int PageSize { get; }
}

public sealed class GetTeamActivitiesQueryHandler(
    IOrganizationDbContext organizationDbContext,
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetTeamActivitiesQuery, Result<PagedResponse<ActivityLogDto>?>>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<Result<PagedResponse<ActivityLogDto>?>> Handle(
        GetTeamActivitiesQuery request, CancellationToken cancellationToken)
    {
        var teamId = await _organizationDbContext.Teams
            .Where(request.IdOrKeyFilter)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (teamId is null)
        {
            return Result.Success<PagedResponse<ActivityLogDto>?>(null);
        }

        var activities = await _activityLogReader.Read(
            teamId.Value,
            aggregateType: "Team",
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
