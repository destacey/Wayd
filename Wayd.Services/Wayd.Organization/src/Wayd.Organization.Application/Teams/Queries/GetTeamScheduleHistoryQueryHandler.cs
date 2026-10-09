using Wayd.Common.Application.Requests.Organization;

namespace Wayd.Organization.Application.Teams.Queries;

public sealed class GetTeamScheduleHistoryQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<GetTeamScheduleHistoryQuery, IReadOnlyList<TeamSchedulePeriodDto>>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;

    public async Task<IReadOnlyList<TeamSchedulePeriodDto>> Handle(GetTeamScheduleHistoryQuery request, CancellationToken cancellationToken)
    {
        return await _organizationDbContext.Teams
            .Where(t => t.Id == request.TeamId)
            .SelectMany(t => t.OperatingModels)
            .OrderBy(m => m.DateRange.Start)
            .Select(m => new TeamSchedulePeriodDto(m.DateRange.Start, m.DateRange.End, m.TimeZone, m.CommitmentGraceDays, m.SizingMethod)
            {
                WorkingWeek = m.WorkingWeek,
                HolidayCalendarId = m.HolidayCalendarId,
            })
            .ToListAsync(cancellationToken);
    }
}
