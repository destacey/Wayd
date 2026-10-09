using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Settings;
using NodaTime;

namespace Wayd.Organization.Application.HolidayCalendars.Queries;

public sealed class GetHolidayDatesQueryHandler(
    IOrganizationDbContext organizationDbContext,
    ISettings<SchedulingSettings> schedulingSettings)
    : IQueryHandler<GetHolidayDatesQuery, IReadOnlyList<LocalDate>>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;

    public async Task<IReadOnlyList<LocalDate>> Handle(GetHolidayDatesQuery request, CancellationToken cancellationToken)
    {
        var calendarId = request.HolidayCalendarId
            ?? (await _schedulingSettings.Get(cancellationToken)).DefaultHolidayCalendarId;
        if (calendarId is null)
            return [];

        var from = request.From;
        var to = request.To;

        return await _organizationDbContext.HolidayCalendars
            .Where(c => c.Id == calendarId)
            .SelectMany(c => c.Holidays)
            .Where(h => h.Date >= from && h.Date <= to)
            .OrderBy(h => h.Date)
            .Select(h => h.Date)
            .ToListAsync(cancellationToken);
    }
}
