using NodaTime;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Models.Planning.Iterations;

namespace Wayd.Work.Domain.Models.SprintScope;

/// <summary>
/// How much of each day of a sprint the team works, which its ideal burn-down falls in proportion to. A day
/// outside the team's working week is not worked at all, and one of the sprint's team days off only for the
/// share of it the team is not off.
/// </summary>
/// <remarks>
/// A team with nobody allocated to it is read as a whole: each day weighs 1 when worked and 0 when it is a
/// holiday in the team's calendar. A team with people allocated weighs a day by the share of their time that
/// is there, so a holiday only some of them take, or one person's time away, lowers the day instead of
/// flattening it.
/// </remarks>
public sealed class SprintWorkingDays
{
    private readonly WorkingWeek _workingWeek;
    private readonly HashSet<LocalDate> _holidays;
    private readonly Dictionary<LocalDate, double> _shareWorkedOnTeamDaysOff;
    private readonly IReadOnlyDictionary<LocalDate, double> _availability;

    /// <summary>
    /// A team read as a whole: every day in its working week weighs 1, except one of <paramref name="holidays"/>,
    /// which weighs 0, and one of <paramref name="teamDaysOff"/>, which weighs the share of it the team works.
    /// </summary>
    /// <param name="holidays">The holidays in the team's calendar.</param>
    /// <param name="teamDaysOff">The sprint's team days off.</param>
    public SprintWorkingDays(WorkingWeek workingWeek, IEnumerable<LocalDate> holidays, IEnumerable<TeamDayOff>? teamDaysOff = null)
        : this(workingWeek, holidays, teamDaysOff ?? [], new Dictionary<LocalDate, double>())
    {
    }

    /// <summary>
    /// A team with people allocated to it: a day in <paramref name="availability"/> weighs the share it
    /// gives, and any other day is read as a whole, as the other constructor reads every day.
    /// </summary>
    /// <remarks>
    /// One sprint can mix the two. A day with no allocation in a role that counts toward delivery capacity,
    /// such as the tail of a sprint after the team's last allocation ends, weighs 1 or 0 by the team's calendar.
    /// Both readings put a normal working day at 1, so the line stays continuous where they meet.
    /// </remarks>
    /// <param name="holidays">The holidays in the team's calendar, for the days read as a whole.</param>
    /// <param name="teamDaysOff">
    /// The sprint's team days off, which take their share of the day whatever the availability: a half day off
    /// halves what is there.
    /// </param>
    /// <param name="availability">
    /// The share of the team's allocated time that is there, from 0 to 1, by day. It already leaves out the
    /// holidays of the calendar each person follows, so the team's calendar is not applied to these days.
    /// </param>
    public SprintWorkingDays(
        WorkingWeek workingWeek,
        IEnumerable<LocalDate> holidays,
        IEnumerable<TeamDayOff> teamDaysOff,
        IReadOnlyDictionary<LocalDate, double> availability)
    {
        _workingWeek = workingWeek;
        _holidays = [.. holidays];
        _shareWorkedOnTeamDaysOff = teamDaysOff
            .GroupBy(d => d.Date)
            .ToDictionary(day => day.Key, day => (double)day.Min(d => d.ShareWorked));
        _availability = availability;
    }

    /// <summary>Every day worked: no weekends, holidays or days off.</summary>
    public static SprintWorkingDays EveryDay { get; } = new(
        WorkingWeek.Create([
            IsoDayOfWeek.Monday,
            IsoDayOfWeek.Tuesday,
            IsoDayOfWeek.Wednesday,
            IsoDayOfWeek.Thursday,
            IsoDayOfWeek.Friday,
            IsoDayOfWeek.Saturday,
            IsoDayOfWeek.Sunday,
        ]).Value,
        []);

    /// <summary>How much of a working day <paramref name="day"/> is, from 0 when the team does not work it to 1 when all of it does.</summary>
    public double Weight(LocalDate day)
    {
        if (!_workingWeek.Includes(day))
            return 0;

        var shareWorked = _shareWorkedOnTeamDaysOff.GetValueOrDefault(day, 1);

        if (_availability.TryGetValue(day, out var share))
            return share * shareWorked;

        return _holidays.Contains(day) ? 0 : shareWorked;
    }
}
