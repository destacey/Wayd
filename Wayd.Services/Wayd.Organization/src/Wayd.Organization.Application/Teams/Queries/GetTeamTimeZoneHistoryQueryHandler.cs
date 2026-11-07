using Wayd.Common.Application.Requests.Organization;

namespace Wayd.Organization.Application.Teams.Queries;

/// <summary>Handles <see cref="GetTeamTimeZoneHistoryQuery"/> for a team or a team of teams.</summary>
public sealed class GetTeamTimeZoneHistoryQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<GetTeamTimeZoneHistoryQuery, IReadOnlyList<TeamTimeZonePeriod>>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;

    public async Task<IReadOnlyList<TeamTimeZonePeriod>> Handle(GetTeamTimeZoneHistoryQuery request, CancellationToken cancellationToken)
    {
        // An id belongs to a team or to a team of teams, never both, so at most one read finds models.
        var teamPeriods = await _organizationDbContext.Teams
            .Where(t => t.Id == request.TeamId)
            .SelectMany(t => t.OperatingModels)
            .Select(m => new TeamTimeZonePeriod(m.DateRange.Start, m.DateRange.End, m.TimeZone))
            .ToListAsync(cancellationToken);

        var periods = teamPeriods.Count > 0
            ? teamPeriods
            : await _organizationDbContext.TeamOfTeams
                .Where(t => t.Id == request.TeamId)
                .SelectMany(t => t.OperatingModels)
                .Select(m => new TeamTimeZonePeriod(m.DateRange.Start, m.DateRange.End, m.TimeZone))
                .ToListAsync(cancellationToken);

        return [.. periods.OrderBy(p => p.Start)];
    }
}
