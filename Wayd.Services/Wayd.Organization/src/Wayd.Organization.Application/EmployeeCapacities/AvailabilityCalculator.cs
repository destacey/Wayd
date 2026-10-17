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

/// <summary>How many hours a working day has: the system's, which is a person-day, and those of the people with their own.</summary>
/// <param name="SystemHours">The system's working day, from the scheduling settings.</param>
/// <param name="HoursByEmployee">The working day of each employee with one of their own, by employee id.</param>
public sealed record WorkingDayLengths(decimal SystemHours, IReadOnlyDictionary<Guid, decimal> HoursByEmployee)
{
    /// <summary>
    /// The decimal places a person-day figure scaled by someone's working day keeps. A 4-hour day against a 7.5-hour
    /// one is a share that never terminates, so without a fixed precision the same time summed by day and summed
    /// over a range would differ in the last digit. Every unscaled figure has fewer places than this, so it is
    /// never rounded.
    /// </summary>
    private const int ScaledDecimals = 10;

    /// <summary>
    /// Nobody with a working day of their own. The system's length is then never read, so it is not given.
    /// </summary>
    public static WorkingDayLengths None { get; } = new(1m, new Dictionary<Guid, decimal>());

    /// <summary>The hours of the employee's own working day, or null when they have the system's.</summary>
    public decimal? OwnHoursOf(Guid employeeId) =>
        HoursByEmployee.TryGetValue(employeeId, out var hours) ? hours : null;

    /// <summary>
    /// What <paramref name="shareOfTheirDays"/>, a number of the employee's own working days, is worth in
    /// person-days: as many for someone with the system's day, half as many for a 4-hour day against an 8-hour one.
    /// </summary>
    public decimal PersonDays(Guid employeeId, decimal shareOfTheirDays) =>
        HoursByEmployee.TryGetValue(employeeId, out var hours)
            ? Math.Round(shareOfTheirDays * hours / SystemHours, ScaledDecimals)
            : shareOfTheirDays;
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
/// a team read as a whole is off. Time away applies whatever team the day belongs to. It also holds how long
/// each person's working day is against the system's, which turns a share of their day into person-days.
/// </remarks>
public sealed class AvailabilityCalculator
{
    private const decimal WholeDay = 100m;

    private readonly IReadOnlyDictionary<Guid, Guid> _calendarByEmployee;
    private readonly IReadOnlyDictionary<Guid, IReadOnlySet<LocalDate>> _holidaysByCalendar;
    private readonly Guid? _defaultCalendarId;
    private readonly ILookup<Guid, TimeAwaySpan> _timeAwayByEmployee;
    private readonly WorkingDayLengths _workingDayLengths;

    /// <param name="calendarByEmployee">The holiday calendar assigned to each employee who has one, by employee id.</param>
    /// <param name="holidaysByCalendar">The holidays of each calendar, by calendar id. A calendar left out has none.</param>
    /// <param name="defaultCalendarId">
    /// The system default calendar, which a person with no calendar assigned and a team with none follow; null
    /// when there is none.
    /// </param>
    /// <param name="workingDayLengths">How long the working days are; null when everyone has the system's.</param>
    public AvailabilityCalculator(
        IReadOnlyDictionary<Guid, Guid> calendarByEmployee,
        IReadOnlyDictionary<Guid, IReadOnlySet<LocalDate>> holidaysByCalendar,
        Guid? defaultCalendarId,
        IEnumerable<TimeAwaySpan> timeAway,
        WorkingDayLengths? workingDayLengths = null)
    {
        _calendarByEmployee = calendarByEmployee;
        _holidaysByCalendar = holidaysByCalendar;
        _defaultCalendarId = defaultCalendarId;
        _timeAwayByEmployee = timeAway.ToLookup(t => t.EmployeeId);
        _workingDayLengths = workingDayLengths ?? WorkingDayLengths.None;
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

    /// <summary>
    /// What <paramref name="shareOfTheirDays"/>, a number of the employee's own working days such as an
    /// allocation's share of one day, is worth in person-days. A person-day is a system working day, so for
    /// someone with a shorter day of their own it is worth less.
    /// </summary>
    public decimal PersonDays(Guid employeeId, decimal shareOfTheirDays) =>
        _workingDayLengths.PersonDays(employeeId, shareOfTheirDays);

    /// <summary>The hours of the employee's own working day, or null when they have the system's.</summary>
    public decimal? OwnWorkingDayHours(Guid employeeId) => _workingDayLengths.OwnHoursOf(employeeId);

    /// <summary>The employee's time away that the calculator was given, in date order.</summary>
    public IReadOnlyList<TimeAwaySpan> TimeAwayOf(Guid employeeId) =>
        [.. _timeAwayByEmployee[employeeId].OrderBy(t => t.Start).ThenBy(t => t.End)];

    private bool IsHolidayIn(Guid calendarId, LocalDate date) =>
        _holidaysByCalendar.TryGetValue(calendarId, out var holidays) && holidays.Contains(date);
}
