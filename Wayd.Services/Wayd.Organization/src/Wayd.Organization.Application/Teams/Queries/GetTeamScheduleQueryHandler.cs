using Wayd.Common.Application.Requests.Organization;

namespace Wayd.Organization.Application.Teams.Queries;

public sealed class GetTeamScheduleQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<GetTeamScheduleQuery, TeamScheduleDto?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;

    public async Task<TeamScheduleDto?> Handle(GetTeamScheduleQuery request, CancellationToken cancellationToken)
    {
        var asOf = request.AsOf;

        return await _organizationDbContext.Teams
            .Where(t => t.Id == request.TeamId)
            .SelectMany(t => t.OperatingModels)
            .Where(m => m.DateRange.Start <= asOf && (m.DateRange.End == null || m.DateRange.End >= asOf))
            .Select(m => new TeamScheduleDto(m.TimeZone, m.CommitmentGraceDays))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
