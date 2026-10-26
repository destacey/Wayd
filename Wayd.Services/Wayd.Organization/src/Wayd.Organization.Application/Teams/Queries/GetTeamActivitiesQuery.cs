using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;

namespace Wayd.Organization.Application.Teams.Queries;

/// <summary>
/// Retrieves activity history for a team, newest first.
/// </summary>
public sealed record GetTeamActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>, IPagedQuery
{
    public GetTeamActivitiesQuery(IdOrKey idOrKey, PageRequest paging)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Team>();
        Paging = paging;
    }

    public Expression<Func<Team, bool>> IdOrKeyFilter { get; }
    public PageRequest Paging { get; }
}

/// <summary>Bounds the page a caller may ask for.</summary>
public sealed class GetTeamActivitiesQueryValidator : PagedQueryValidator<GetTeamActivitiesQuery>;

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
            paging: request.Paging,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
