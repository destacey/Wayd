namespace Wayd.Work.Application.WorkTeams;

/// <summary>
/// The calendar periods a team report splits a date range into, so every report over the same range
/// lines up. A team of teams' own teams may run different iteration cadences, so iterations cannot be
/// the period.
/// </summary>
public static class ReportPeriods
{
    /// <summary>
    /// Weeks up to a month, two weeks up to six months, otherwise calendar months. The periods are
    /// contiguous, cover <paramref name="from"/> to <paramref name="to"/> inclusive, and the last is cut
    /// short at <paramref name="to"/>.
    /// </summary>
    public static List<(LocalDate Start, LocalDate End)> Between(LocalDate from, LocalDate to)
    {
        var periods = new List<(LocalDate, LocalDate)>();
        var days = Period.Between(from, to, PeriodUnits.Days).Days + 1;

        if (days > 184)
        {
            for (var start = from; start <= to;)
            {
                var monthEnd = start.With(DateAdjusters.EndOfMonth);
                var end = monthEnd < to ? monthEnd : to;
                periods.Add((start, end));
                start = end.PlusDays(1);
            }

            return periods;
        }

        var size = days <= 31 ? 7 : 14;
        for (var start = from; start <= to; start = start.PlusDays(size))
        {
            var end = start.PlusDays(size - 1);
            periods.Add((start, end < to ? end : to));
        }

        return periods;
    }
}
