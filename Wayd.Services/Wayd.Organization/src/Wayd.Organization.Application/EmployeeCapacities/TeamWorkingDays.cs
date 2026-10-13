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
        var workingDays = await Load(organizationDbContext, dispatcher, schedulingSettings, [teamId], from, to, cancellationToken);
        return workingDays[teamId];
    }

    /// <summary>
    /// The working days of each of <paramref name="teamIds"/> from <paramref name="from"/> to
    /// <paramref name="to"/>, by team id, each as the single-team read gives them. Every id asked for has an
    /// entry, whatever it names.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<LocalDate>>> Load(
        IOrganizationDbContext organizationDbContext,
        IDispatcher dispatcher,
        ISettings<SchedulingSettings> schedulingSettings,
        IReadOnlyCollection<Guid> teamIds,
        LocalDate from,
        LocalDate to,
        CancellationToken cancellationToken)
    {
        var periodsByTeam = (await organizationDbContext.Teams
            .Where(t => teamIds.Contains(t.Id))
            .SelectMany(t => t.OperatingModels, (t, m) => new { TeamId = t.Id, Model = m })
            .Where(x => x.Model.DateRange.Start <= to && (x.Model.DateRange.End == null || x.Model.DateRange.End >= from))
            .Select(x => new { x.TeamId, x.Model.DateRange.Start, x.Model.DateRange.End, x.Model.WorkingWeek, x.Model.HolidayCalendarId })
            .ToListAsync(cancellationToken))
            .ToLookup(p => p.TeamId);

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

        Dictionary<Guid, IReadOnlyList<LocalDate>> workingDaysByTeam = [];
        foreach (var teamId in teamIds.Distinct())
        {
            var periods = periodsByTeam[teamId].ToList();

            List<LocalDate> workingDays = [];
            for (var day = from; day <= to; day = day.PlusDays(1))
            {
                var period = periods.Find(p => p.Start <= day && (p.End is null || p.End >= day));
                var workingWeek = period?.WorkingWeek ?? defaultWorkingWeek;

                if (workingWeek.Includes(day) && !(await Holidays(period?.HolidayCalendarId)).Contains(day))
                    workingDays.Add(day);
            }

            workingDaysByTeam[teamId] = workingDays;
        }

        return workingDaysByTeam;
    }
}
