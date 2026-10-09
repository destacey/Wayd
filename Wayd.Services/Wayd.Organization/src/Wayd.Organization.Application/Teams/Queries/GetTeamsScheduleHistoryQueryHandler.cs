using Wayd.Common.Application.Requests.Organization;

namespace Wayd.Organization.Application.Teams.Queries;

public sealed class GetTeamsScheduleHistoryQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<GetTeamsScheduleHistoryQuery, IReadOnlyDictionary<Guid, IReadOnlyList<TeamSchedulePeriodDto>>>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<TeamSchedulePeriodDto>>> Handle(GetTeamsScheduleHistoryQuery request, CancellationToken cancellationToken)
    {
        if (request.TeamIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<TeamSchedulePeriodDto>>();

        var periods = await _organizationDbContext.Teams
            .Where(t => request.TeamIds.Contains(t.Id))
            .SelectMany(t => t.OperatingModels, (t, m) => new
            {
                TeamId = t.Id,
                Period = new TeamSchedulePeriodDto(m.DateRange.Start, m.DateRange.End, m.TimeZone, m.CommitmentGraceDays, m.SizingMethod)
                {
                    WorkingWeek = m.WorkingWeek,
                    HolidayCalendarId = m.HolidayCalendarId,
                },
            })
            .ToListAsync(cancellationToken);

        return periods
            .GroupBy(p => p.TeamId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<TeamSchedulePeriodDto>)[.. g.Select(p => p.Period).OrderBy(p => p.Start)]);
    }
}
