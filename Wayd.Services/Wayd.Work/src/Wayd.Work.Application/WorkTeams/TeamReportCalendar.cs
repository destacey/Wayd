using Wayd.Common.Application.Requests.Organization;

namespace Wayd.Work.Application.WorkTeams;

/// <summary>
/// The days of the team a report is for, counted in the time zone of its operating model in effect on each
/// day. A team of teams' report counts every team beneath it in the team of teams' own zone.
/// </summary>
/// <remarks>
/// A day runs from <see cref="StartOf"/> that date up to <see cref="StartOf"/> the next, so a range starts in the
/// zone in effect on its first day and ends in the zone in effect on its last, and <see cref="DateOf"/> always
/// puts an instant in the day whose bounds hold it. The one exception is a change of zone more than a day east
/// (UTC−11 to UTC+14), after which the new zone's first day starts before the old zone's last: an instant in
/// the overlap is dated on the old day and so can fall outside a range that the bounds place it in.
/// </remarks>
public sealed class TeamReportCalendar
{
    private readonly IReadOnlyList<(LocalDate Start, DateTimeZone Zone)> _periods;

    private TeamReportCalendar(IReadOnlyList<(LocalDate Start, DateTimeZone Zone)> periods)
    {
        _periods = periods;
    }

    /// <summary>A calendar of UTC days, for a team with no operating model.</summary>
    public static TeamReportCalendar Utc { get; } = new([]);

    /// <summary>
    /// The calendar of <paramref name="periods"/>; UTC days when there are none. A zone NodaTime does not know
    /// reads as UTC.
    /// </summary>
    public static TeamReportCalendar From(IEnumerable<TeamTimeZonePeriod>? periods) =>
        new([.. (periods ?? [])
            .OrderBy(p => p.Start)
            .Select(p => (p.Start, DateTimeZoneProviders.Tzdb.GetZoneOrNull(p.TimeZone) ?? DateTimeZone.Utc))]);

    /// <summary>Loads the calendar of the team or team of teams with <paramref name="teamId"/>.</summary>
    public static async Task<TeamReportCalendar> Load(IDispatcher dispatcher, Guid teamId, CancellationToken cancellationToken) =>
        From(await dispatcher.Send(new GetTeamTimeZoneHistoryQuery(teamId), cancellationToken));

    /// <summary>
    /// The zone in effect on <paramref name="date"/>. A date before the first operating model reads in that
    /// model's zone.
    /// </summary>
    public DateTimeZone ZoneOn(LocalDate date)
    {
        if (_periods.Count == 0)
            return DateTimeZone.Utc;

        var zone = _periods[0].Zone;
        foreach (var (start, periodZone) in _periods)
        {
            if (start > date)
                break;
            zone = periodZone;
        }

        return zone;
    }

    /// <summary>The first instant of <paramref name="date"/>.</summary>
    public Instant StartOf(LocalDate date) => date.AtStartOfDayInZone(ZoneOn(date)).ToInstant();

    /// <summary>The day <paramref name="instant"/> falls in.</summary>
    public LocalDate DateOf(Instant instant)
    {
        var date = instant.InZone(ZoneOn(instant.InUtc().Date)).Date;

        // Near a change of zone the guess can be a day out; no two zones are more than two days apart.
        for (var i = 0; i < 2 && StartOf(date) > instant; i++)
            date = date.PlusDays(-1);
        for (var i = 0; i < 2 && StartOf(date.PlusDays(1)) <= instant; i++)
            date = date.PlusDays(1);

        return date;
    }
}
