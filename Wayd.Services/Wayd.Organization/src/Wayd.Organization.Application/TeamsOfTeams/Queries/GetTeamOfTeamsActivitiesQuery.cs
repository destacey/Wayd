using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;

namespace Wayd.Organization.Application.TeamsOfTeams.Queries;

/// <summary>
/// Retrieves activity history for a team of teams, newest first.
/// </summary>
public sealed record GetTeamOfTeamsActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>
{
    public GetTeamOfTeamsActivitiesQuery(IdOrKey idOrKey, int page = 1, int pageSize = 50)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<TeamOfTeams>();
        Page = page;
        PageSize = pageSize;
    }

    public Expression<Func<TeamOfTeams, bool>> IdOrKeyFilter { get; }
    public int Page { get; }
    public int PageSize { get; }
}

public sealed class GetTeamOfTeamsActivitiesQueryHandler(
    IOrganizationDbContext organizationDbContext,
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetTeamOfTeamsActivitiesQuery, Result<PagedResponse<ActivityLogDto>?>>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<Result<PagedResponse<ActivityLogDto>?>> Handle(
        GetTeamOfTeamsActivitiesQuery request, CancellationToken cancellationToken)
    {
        var teamOfTeamsId = await _organizationDbContext.TeamOfTeams
            .Where(request.IdOrKeyFilter)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (teamOfTeamsId is null)
        {
            return Result.Success<PagedResponse<ActivityLogDto>?>(null);
        }

        // A team of teams raises the same Team* events as a team, so its entries carry the "Team"
        // aggregate type. The id is what separates the two.
        var activities = await _activityLogReader.Read(
            teamOfTeamsId.Value,
            aggregateType: "Team",
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
