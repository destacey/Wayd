using NodaTime;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Settings;

namespace Wayd.Organization.Application.EmployeeCapacities;

/// <summary>The days a team's working week covers, which its capacity is counted over.</summary>
internal static class TeamWorkingDays
{
    /// <summary>
    /// The days of a team's working week from <paramref name="from"/> to <paramref name="to"/>, both
    /// inclusive, in date order, each with the holiday calendar of the operating model in effect that day. A
    /// team of teams, and any day a team had no operating model, takes the system default working week and
    /// calendar.
    /// </summary>
    /// <remarks>
    /// Holidays are not taken out here. Each person follows the calendar assigned to them or the system
    /// default, whatever the team's; the team's calendar only gives the team's own working-day count.
    /// <see cref="AvailabilityCalculator"/> decides both.
    /// </remarks>
    public static async Task<IReadOnlyList<TeamDay>> Load(
        IOrganizationDbContext organizationDbContext,
        ISettings<SchedulingSettings> schedulingSettings,
        Guid teamId,
        LocalDate from,
        LocalDate to,
        CancellationToken cancellationToken)
    {
        var teamDays = await Load(organizationDbContext, schedulingSettings, [teamId], from, to, cancellationToken);
        return teamDays[teamId];
    }

    /// <summary>
    /// The days of each of <paramref name="teamIds"/>' working week from <paramref name="from"/> to
    /// <paramref name="to"/>, by team id, each as the single-team read gives them. Every id asked for has an
    /// entry, whatever it names.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<TeamDay>>> Load(
        IOrganizationDbContext organizationDbContext,
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

        Dictionary<Guid, IReadOnlyList<TeamDay>> teamDaysByTeam = [];
        foreach (var teamId in teamIds.Distinct())
        {
            var periods = periodsByTeam[teamId].ToList();

            List<TeamDay> teamDays = [];
            for (var day = from; day <= to; day = day.PlusDays(1))
            {
                var period = periods.Find(p => p.Start <= day && (p.End is null || p.End >= day));
                var workingWeek = period?.WorkingWeek ?? defaultWorkingWeek;

                if (workingWeek.Includes(day))
                    teamDays.Add(new TeamDay(day, period?.HolidayCalendarId));
            }

            teamDaysByTeam[teamId] = teamDays;
        }

        return teamDaysByTeam;
    }
}
