using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings.Scheduling.Dtos;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.Scheduling.Queries;

public sealed record GetSchedulingSettingsQuery : IQuery<SchedulingSettingsDto>;

public sealed class GetSchedulingSettingsQueryHandler(ISettings<SchedulingSettings> settings, IDispatcher dispatcher)
    : IQueryHandler<GetSchedulingSettingsQuery, SchedulingSettingsDto>
{
    private readonly ISettings<SchedulingSettings> _settings = settings;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<SchedulingSettingsDto> Handle(GetSchedulingSettingsQuery request, CancellationToken cancellationToken)
    {
        var values = await _settings.Get(cancellationToken);

        return new SchedulingSettingsDto
        {
            DefaultTimeZone = values.DefaultTimeZone,
            DefaultCommitmentGraceDays = values.DefaultCommitmentGraceDays,
            DefaultWorkingDays = values.DefaultWorkingDays,
            DefaultHolidayCalendar = values.DefaultHolidayCalendarId is { } calendarId
                ? await _dispatcher.Send(new GetHolidayCalendarNavigationQuery(calendarId), cancellationToken)
                : null,
        };
    }
}
