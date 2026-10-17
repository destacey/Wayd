using NodaTime;
using Wayd.Common.Domain.Models.Planning.Iterations;

namespace Wayd.Organization.Application.EmployeeCapacities;

/// <summary>A day in a team's working week, with the holiday calendar the team takes that day.</summary>
/// <param name="HolidayCalendarId">
/// The team's calendar that day, or null for the system default calendar. It decides the team's own working-day
/// count, never a person's holidays.
/// </param>
/// <param name="ShareWorked">
/// The share of the day the team works, from 0 to 1: less than 1 on a part team day off, which takes that much of
/// everyone's time.
/// </param>
public readonly record struct TeamDay(LocalDate Date, Guid? HolidayCalendarId, decimal ShareWorked = 1m)
{
    /// <summary>
    /// <paramref name="days"/> with the team's <paramref name="daysOff"/> taken out of the share each is worked:
    /// a full day off is kept at 0, so nobody is counted on it. A day listed twice takes the larger share off.
    /// </summary>
    public static IReadOnlyList<TeamDay> WithDaysOff(IEnumerable<TeamDay> days, IEnumerable<TeamDayOff> daysOff)
    {
        var shareWorked = daysOff
            .GroupBy(d => d.Date)
            .ToDictionary(day => day.Key, day => day.Min(d => d.ShareWorked));
        if (shareWorked.Count == 0)
            return [.. days];

        return [.. days.Select(d => shareWorked.TryGetValue(d.Date, out var share) ? d with { ShareWorked = d.ShareWorked * share } : d)];
    }
}

/// <summary>A stretch of days an employee is away, as the availability sums read it.</summary>
/// <param name="PercentAway">The share of each day the employee is away, from 1 to 100.</param>
public sealed record TimeAwaySpan(Guid EmployeeId, LocalDate Start, LocalDate End, int PercentAway);

/// <summary>
/// How much of a team's working day a person is there for: none of it on a holiday in the calendar they take,
/// and otherwise what their time away leaves. Every capacity figure and the sprint day weight read a person's
/// availability from here, so they agree. Pure: everything it needs is passed in.
/// </summary>
/// <remarks>
/// A person's holidays do not depend on which team is asking: they follow the holiday calendar assigned to
/// them, and otherwise the system default. A team's own calendar plays no part here; it only decides the days
/// a team read as a whole is off. Time away applies whatever team the day belongs to.
/// </remarks>
public sealed class AvailabilityCalculator
{
    private const decimal WholeDay = 100m;

    private readonly IReadOnlyDictionary<Guid, Guid> _calendarByEmployee;
    private readonly IReadOnlyDictionary<Guid, IReadOnlySet<LocalDate>> _holidaysByCalendar;
    private readonly Guid? _defaultCalendarId;
    private readonly ILookup<Guid, TimeAwaySpan> _timeAwayByEmployee;

    /// <param name="calendarByEmployee">The holiday calendar assigned to each employee who has one, by employee id.</param>
    /// <param name="holidaysByCalendar">The holidays of each calendar, by calendar id. A calendar left out has none.</param>
    /// <param name="defaultCalendarId">
    /// The system default calendar, which a person with no calendar assigned and a team with none follow; null
    /// when there is none.
    /// </param>
    public AvailabilityCalculator(
        IReadOnlyDictionary<Guid, Guid> calendarByEmployee,
        IReadOnlyDictionary<Guid, IReadOnlySet<LocalDate>> holidaysByCalendar,
        Guid? defaultCalendarId,
        IEnumerable<TimeAwaySpan> timeAway)
    {
        _calendarByEmployee = calendarByEmployee;
        _holidaysByCalendar = holidaysByCalendar;
        _defaultCalendarId = defaultCalendarId;
        _timeAwayByEmployee = timeAway.ToLookup(t => t.EmployeeId);
    }

    /// <summary>Everyone there every working day: no holidays and no time away.</summary>
    public static AvailabilityCalculator AlwaysAvailable { get; } =
        new(new Dictionary<Guid, Guid>(), new Dictionary<Guid, IReadOnlySet<LocalDate>>(), null, []);

    /// <summary>
    /// The share of <paramref name="day"/> the employee is there for, from 0 to 1: 0 on a holiday in the
    /// calendar they take, and otherwise 1 less the share of the day they are away, of the share the team works.
    /// </summary>
    public decimal Available(Guid employeeId, TeamDay day) =>
        IsHoliday(employeeId, day) ? 0m : (1m - ShareAway(employeeId, day.Date)) * day.ShareWorked;

    /// <summary>
    /// Whether <paramref name="day"/> is a holiday for the employee: in the calendar assigned to them when they
    /// have one, and otherwise in the system default. The same on every team they are allocated to.
    /// </summary>
    public bool IsHoliday(Guid employeeId, TeamDay day) =>
        (_calendarByEmployee.TryGetValue(employeeId, out var assigned) ? assigned : _defaultCalendarId) is { } calendarId
        && IsHolidayIn(calendarId, day.Date);

    /// <summary>
    /// Whether <paramref name="day"/> is a holiday in the team's calendar, or in the system default when the team
    /// has none: a day the team read as a whole is off. It never decides a person's holidays.
    /// </summary>
    public bool IsTeamHoliday(TeamDay day) =>
        (day.HolidayCalendarId ?? _defaultCalendarId) is { } calendarId && IsHolidayIn(calendarId, day.Date);

    /// <summary>
    /// The share of <paramref name="date"/> the employee is away, from 0 to 1. Stretches that overlap add up,
    /// to no more than the whole day.
    /// </summary>
    public decimal ShareAway(Guid employeeId, LocalDate date)
    {
        var percent = _timeAwayByEmployee[employeeId]
            .Where(t => t.Start <= date && date <= t.End)
            .Sum(t => t.PercentAway);

        return Math.Min(percent, WholeDay) / WholeDay;
    }

    /// <summary>The employee's time away that the calculator was given, in date order.</summary>
    public IReadOnlyList<TimeAwaySpan> TimeAwayOf(Guid employeeId) =>
        [.. _timeAwayByEmployee[employeeId].OrderBy(t => t.Start).ThenBy(t => t.End)];

    private bool IsHolidayIn(Guid calendarId, LocalDate date) =>
        _holidaysByCalendar.TryGetValue(calendarId, out var holidays) && holidays.Contains(date);
}
