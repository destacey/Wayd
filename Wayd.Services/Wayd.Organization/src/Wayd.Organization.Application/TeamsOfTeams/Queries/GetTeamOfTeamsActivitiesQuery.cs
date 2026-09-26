using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Dtos;
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

        return Result.Success<PagedResponse<ActivityLogDto>?>(await ResolveRaisedOn(activities, cancellationToken));
    }

    /// <summary>
    /// Names the child team or team of teams each related entry was raised on, so the section can say
    /// where it came from.
    /// </summary>
    private async Task<PagedResponse<ActivityLogDto>> ResolveRaisedOn(
        PagedResponse<ActivityLogDto> activities, CancellationToken cancellationToken)
    {
        var teamIds = activities.Items
            .Where(a => a.IsRelated && a.AggregateType == "Team")
            .Select(a => a.AggregateId)
            .Distinct()
            .ToList();

        if (teamIds.Count == 0)
        {
            return activities;
        }

        var teams = await _organizationDbContext.BaseTeams
            .Where(t => teamIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Key, t.Name })
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        return activities with
        {
            Items = [.. activities.Items.Select(a =>
                a.IsRelated && a.AggregateType == "Team" && teams.TryGetValue(a.AggregateId, out var team)
                    ? a with { RaisedOn = NavigationDto.Create(team.Id, team.Key, team.Name) }
                    : a)],
        };
    }
}
