using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Settings;
using Wayd.Organization.Application.HolidayCalendars.Dtos;

namespace Wayd.Organization.Application.HolidayCalendars.Queries;

/// <summary>Every holiday calendar, by name.</summary>
public sealed record GetHolidayCalendarsQuery : IQuery<IReadOnlyList<HolidayCalendarListDto>>;

public sealed class GetHolidayCalendarsQueryHandler(
    IOrganizationDbContext organizationDbContext,
    ISettings<SchedulingSettings> schedulingSettings)
    : IQueryHandler<GetHolidayCalendarsQuery, IReadOnlyList<HolidayCalendarListDto>>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;

    public async Task<IReadOnlyList<HolidayCalendarListDto>> Handle(GetHolidayCalendarsQuery request, CancellationToken cancellationToken)
    {
        var defaultId = (await _schedulingSettings.Get(cancellationToken)).DefaultHolidayCalendarId;

        return await _organizationDbContext.HolidayCalendars
            .OrderBy(c => c.Name)
            .Select(c => new HolidayCalendarListDto
            {
                Id = c.Id,
                Key = c.Key,
                Name = c.Name,
                Description = c.Description,
                HolidayCount = c.Holidays.Count,
                IsDefault = c.Id == defaultId,
            })
            .ToListAsync(cancellationToken);
    }
}
