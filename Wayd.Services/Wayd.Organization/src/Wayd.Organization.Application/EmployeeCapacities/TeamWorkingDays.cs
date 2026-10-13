using NodaTime;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Settings;

namespace Wayd.Organization.Application.EmployeeCapacities;

/// <summary>The days a team works, which its capacity is counted over.</summary>
internal static class TeamWorkingDays
{
    /// <summary>
    /// The working days of a team from <paramref name="from"/> to <paramref name="to"/>, both inclusive, in
    /// date order: each day in the working week of the operating model in effect that day, less that model's
    /// holidays. A team of teams, and any day a team had no operating model, takes the system default working
    /// week and holiday calendar.
    /// </summary>
    public static async Task<IReadOnlyList<LocalDate>> Load(
        IOrganizationDbContext organizationDbContext,
        IDispatcher dispatcher,
        ISettings<SchedulingSettings> schedulingSettings,
        Guid teamId,
        LocalDate from,
        LocalDate to,
        CancellationToken cancellationToken)
    {
        var periods = await organizationDbContext.Teams
            .Where(t => t.Id == teamId)
            .SelectMany(t => t.OperatingModels)
            .Where(m => m.DateRange.Start <= to && (m.DateRange.End == null || m.DateRange.End >= from))
            .Select(m => new { m.DateRange.Start, m.DateRange.End, m.WorkingWeek, m.HolidayCalendarId })
            .ToListAsync(cancellationToken);

        var defaultWorkingWeek = (await schedulingSettings.Get(cancellationToken)).DefaultWorkingWeek();

        // Keyed by calendar id, with Guid.Empty standing for the system default calendar.
        Dictionary<Guid, HashSet<LocalDate>> holidaysByCalendar = [];
        async Task<HashSet<LocalDate>> Holidays(Guid? calendarId)
        {
            var key = calendarId ?? Guid.Empty;
            if (!holidaysByCalendar.TryGetValue(key, out var holidays))
            {
                holidays = [.. await dispatcher.Send(new GetHolidayDatesQuery(calendarId, from, to), cancellationToken)];
                holidaysByCalendar[key] = holidays;
            }

            return holidays;
        }

        List<LocalDate> workingDays = [];
        for (var day = from; day <= to; day = day.PlusDays(1))
        {
            var period = periods.Find(p => p.Start <= day && (p.End is null || p.End >= day));
            var workingWeek = period?.WorkingWeek ?? defaultWorkingWeek;

            if (workingWeek.Includes(day) && !(await Holidays(period?.HolidayCalendarId)).Contains(day))
                workingDays.Add(day);
        }

        return workingDays;
    }
}
