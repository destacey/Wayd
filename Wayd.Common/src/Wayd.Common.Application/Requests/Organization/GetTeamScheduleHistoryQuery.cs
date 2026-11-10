using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Organizations;

namespace Wayd.Common.Application.Requests.Organization;

/// <summary>
/// The time zone, commitment grace period, sizing method, working week and holiday calendar of every operating
/// model an Organization team has had, in date order. Empty when the team does not exist.
/// </summary>
/// <remarks>
/// For reading many sprints of one team at once; <see cref="GetTeamScheduleQuery"/> reads one day.
/// </remarks>
public sealed record GetTeamScheduleHistoryQuery(Guid TeamId) : IQuery<IReadOnlyList<TeamSchedulePeriodDto>>;

/// <summary>
/// <see cref="GetTeamScheduleHistoryQuery"/> for several teams in one read, keyed by team id. A team that does
/// not exist or has no operating models is missing from the result.
/// </summary>
public sealed record GetTeamsScheduleHistoryQuery(IReadOnlyCollection<Guid> TeamIds) : IQuery<IReadOnlyDictionary<Guid, IReadOnlyList<TeamSchedulePeriodDto>>>;

/// <param name="End">Inclusive; null for the current operating model.</param>
/// <param name="SizingMethod">Which of a work item's estimates the team's sprints in the period are measured in.</param>
public sealed record TeamSchedulePeriodDto(LocalDate Start, LocalDate? End, string TimeZone, int CommitmentGraceDays, SizingMethod SizingMethod)
{
    /// <summary>
    /// How the team works in the period: a Scrum team is measured by its sprints, a Kanban team by dates.
    /// </summary>
    public Methodology Methodology { get; init; } = Methodology.Scrum;

    /// <summary>The days of the week the team works in the period.</summary>
    public WorkingWeek WorkingWeek { get; init; } = WorkingWeek.MondayToFriday;

    /// <summary>
    /// The holiday calendar whose holidays the team takes off in the period, or null for the system default
    /// calendar, which <see cref="GetHolidayDatesQuery"/> resolves when read.
    /// </summary>
    public Guid? HolidayCalendarId { get; init; }

    /// <summary>
    /// The periods a Kanban team's velocity is reported in during the period, or null for none, which is always
    /// so for a Scrum team.
    /// </summary>
    public ReportingCadence? Cadence { get; init; }

    /// <summary>Whether <paramref name="date"/> falls in this period.</summary>
    public bool IncludesDate(LocalDate date) => Start <= date && (End is null || date <= End);
}

/// <summary>Reading a team's schedule history.</summary>
public static class TeamSchedulePeriodExtensions
{
    /// <summary>
    /// The operating model in effect at <paramref name="now"/>: the latest one to have started by today's date in its
    /// own time zone, so a model that starts tomorrow in UTC but today in the team's zone already applies. Where a
    /// change of zone leaves two models both claiming today, the newer one wins; where a move west leaves neither
    /// claiming it, the older one holds until the newer one starts. Null when none covers today.
    /// </summary>
    /// <param name="periods">The team's operating models, in date order.</param>
    public static TeamSchedulePeriodDto? InEffectAt(this IReadOnlyList<TeamSchedulePeriodDto> periods, Instant now)
    {
        for (var i = periods.Count - 1; i >= 0; i--)
        {
            var period = periods[i];
            var today = now.InZone(period.Zone()).Date;
            if (period.Start > today)
                continue;
            if (period.IncludesDate(today))
                return period;

            // Ended in its own zone, while the model that follows it, from the next day, has not begun in its own.
            var next = i + 1 < periods.Count ? periods[i + 1] : null;
            return next is not null && period.End is { } end && next.Start == end.PlusDays(1) ? period : null;
        }

        return null;
    }

    /// <summary>
    /// The zone the period's days are counted in. Zones are validated when saved; an id the tz database later
    /// drops reads as UTC rather than failing every read of the team.
    /// </summary>
    public static DateTimeZone Zone(this TeamSchedulePeriodDto period) =>
        DateTimeZoneProviders.Tzdb.GetZoneOrNull(period.TimeZone) ?? DateTimeZone.Utc;
}
