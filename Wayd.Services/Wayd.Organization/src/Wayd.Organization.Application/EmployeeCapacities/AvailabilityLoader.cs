using NodaTime;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Settings;

namespace Wayd.Organization.Application.EmployeeCapacities;

/// <summary>Reads what a person's availability is worked out from: the calendars people take, their holidays, and time away.</summary>
internal static class AvailabilityLoader
{
    /// <summary>
    /// An <see cref="AvailabilityCalculator"/> for <paramref name="employeeIds"/> from <paramref name="from"/>
    /// to <paramref name="to"/>, both inclusive.
    /// </summary>
    /// <param name="teamCalendarIds">
    /// The calendars of the teams whose working-day count will be asked for; null is the system default. Empty
    /// when only people's availability is wanted.
    /// </param>
    /// <remarks>
    /// Three reads however many people and calendars there are: who has a calendar assigned, everyone's time
    /// away, and the holidays of every calendar in play.
    /// </remarks>
    public static async Task<AvailabilityCalculator> Load(
        IOrganizationDbContext organizationDbContext,
        ISettings<SchedulingSettings> schedulingSettings,
        IReadOnlyCollection<Guid> employeeIds,
        IEnumerable<Guid?> teamCalendarIds,
        LocalDate from,
        LocalDate to,
        CancellationToken cancellationToken)
    {
        var defaultCalendarId = (await schedulingSettings.Get(cancellationToken)).DefaultHolidayCalendarId;

        var calendarByEmployee = await organizationDbContext.EmployeeCapacities
            .Where(c => employeeIds.Contains(c.Id) && c.HolidayCalendarId != null)
            .Select(c => new { c.Id, CalendarId = c.HolidayCalendarId!.Value })
            .ToDictionaryAsync(c => c.Id, c => c.CalendarId, cancellationToken);

        var timeAway = await organizationDbContext.EmployeeTimeAway
            .Where(t => employeeIds.Contains(t.EmployeeId) && t.Start <= to && t.End >= from)
            .Select(t => new TimeAwaySpan(t.EmployeeId, t.Start, t.End, t.PercentAway))
            .ToListAsync(cancellationToken);

        List<Guid> calendarIds = [.. teamCalendarIds
            .Append(defaultCalendarId)
            .OfType<Guid>()
            .Concat(calendarByEmployee.Values)
            .Distinct()];

        var holidays = calendarIds.Count == 0
            ? []
            : await organizationDbContext.HolidayCalendars
                .Where(c => calendarIds.Contains(c.Id))
                .SelectMany(c => c.Holidays, (c, h) => new { CalendarId = c.Id, h.Date })
                .Where(h => h.Date >= from && h.Date <= to)
                .ToListAsync(cancellationToken);

        var holidaysByCalendar = holidays
            .GroupBy(h => h.CalendarId)
            .ToDictionary(g => g.Key, g => (IReadOnlySet<LocalDate>)g.Select(h => h.Date).ToHashSet());

        return new AvailabilityCalculator(calendarByEmployee, holidaysByCalendar, defaultCalendarId, timeAway);
    }
}
