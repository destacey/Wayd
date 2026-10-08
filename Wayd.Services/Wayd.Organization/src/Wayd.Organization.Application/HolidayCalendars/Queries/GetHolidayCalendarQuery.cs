using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Settings;
using Wayd.Organization.Application.HolidayCalendars.Dtos;

namespace Wayd.Organization.Application.HolidayCalendars.Queries;

/// <summary>A holiday calendar with its holidays, by id or key; null when there is none.</summary>
public sealed record GetHolidayCalendarQuery : IQuery<HolidayCalendarDetailsDto?>
{
    public GetHolidayCalendarQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<HolidayCalendar>();
    }

    public Expression<Func<HolidayCalendar, bool>> IdOrKeyFilter { get; }
}

public sealed class GetHolidayCalendarQueryHandler(
    IOrganizationDbContext organizationDbContext,
    ISettings<SchedulingSettings> schedulingSettings)
    : IQueryHandler<GetHolidayCalendarQuery, HolidayCalendarDetailsDto?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;

    public async Task<HolidayCalendarDetailsDto?> Handle(GetHolidayCalendarQuery request, CancellationToken cancellationToken)
    {
        var defaultId = (await _schedulingSettings.Get(cancellationToken)).DefaultHolidayCalendarId;

        var calendar = await _organizationDbContext.HolidayCalendars
            .Where(request.IdOrKeyFilter)
            .Select(c => new
            {
                c.Id,
                c.Key,
                c.Name,
                c.Description,
                Holidays = c.Holidays
                    .OrderBy(h => h.Date)
                    .Select(h => new HolidayDto { Id = h.Id, Date = h.Date, Name = h.Name })
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (calendar is null)
            return null;

        var operatingModelCount = await _organizationDbContext.TeamOperatingModels
            .CountAsync(m => m.HolidayCalendarId == calendar.Id, cancellationToken);

        return new HolidayCalendarDetailsDto
        {
            Id = calendar.Id,
            Key = calendar.Key,
            Name = calendar.Name,
            Description = calendar.Description,
            IsDefault = calendar.Id == defaultId,
            OperatingModelCount = operatingModelCount,
            Holidays = calendar.Holidays,
        };
    }
}
