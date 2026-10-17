using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Organizations;

namespace Wayd.Work.Domain.Models.CadenceMetrics;

/// <summary>
/// One operating model of a Kanban team that has a reporting cadence: the days it covers, and how the team's
/// days and work were counted then.
/// </summary>
/// <param name="End">The model's last day, included in it; null for the current model.</param>
/// <param name="TimeZone">The IANA id of the zone the model's days are counted in.</param>
/// <param name="SizingMethod">The estimate the team's work was measured in under this model.</param>
public sealed record CadenceSchedulePeriod(LocalDate Start, LocalDate? End, string TimeZone, SizingMethod SizingMethod, ReportingCadence Cadence);

/// <summary>
/// One of a Kanban team's reporting periods: a stretch of its cadence, cut short where an operating model starts
/// or ends partway through it.
/// </summary>
/// <param name="Start">The period's first day.</param>
/// <param name="End">The period's last day, included in it.</param>
/// <param name="SizingMethod">The estimate the period's work is measured in: its operating model's.</param>
/// <param name="IsPartial">
/// Whether the period is shorter than its cadence, because an operating model began or ended inside it. A
/// partial period is shown but never averaged, since it would read as a slow period.
/// </param>
/// <param name="IsCurrent">Whether the period has not ended yet, so its work is still coming in.</param>
/// <param name="From">When the period starts: the start of its first day in its operating model's zone.</param>
/// <param name="Until">When the period ends: the start of the day after its last, in its operating model's zone.</param>
public sealed record CadencePeriod(
    LocalDate Start,
    LocalDate End,
    SizingMethod SizingMethod,
    bool IsPartial,
    bool IsCurrent,
    Instant From,
    Instant Until)
{
    /// <summary>
    /// The team's reporting periods: its current period, if any, then up to <paramref name="window"/> of the
    /// periods that have ended by <paramref name="now"/>, most recent first.
    /// </summary>
    /// <remarks>
    /// Each operating model's periods follow its own cadence and stop where the model does, so a change of cadence
    /// closes the open period the day before the new model starts and leaves earlier periods as they were. Days
    /// under a model with no cadence belong to no period.
    /// </remarks>
    public static IReadOnlyList<CadencePeriod> Recent(IEnumerable<CadenceSchedulePeriod> schedule, Instant now, int window)
    {
        var periods = new List<CadencePeriod>();

        foreach (var model in schedule.OrderByDescending(m => m.Start))
        {
            var zone = DateTimeZoneProviders.Tzdb[model.TimeZone];
            var today = now.InZone(zone).Date;
            if (model.Start > today)
                continue;

            var last = model.End is { } end && end < today ? end : today;

            foreach (var full in model.Cadence.PeriodsOverlapping(model.Start, last).Reverse())
            {
                if (periods.Count(p => !p.IsCurrent) >= window)
                    return periods;

                var start = full.Start < model.Start ? model.Start : full.Start;
                var finish = model.End is { } modelEnd && full.End > modelEnd ? modelEnd : full.End;
                var from = start.AtStartOfDayInZone(zone).ToInstant();
                var until = finish.PlusDays(1).AtStartOfDayInZone(zone).ToInstant();

                periods.Add(new CadencePeriod(
                    start,
                    finish,
                    model.SizingMethod,
                    IsPartial: start != full.Start || finish != full.End,
                    IsCurrent: until > now,
                    from,
                    until));
            }
        }

        return periods;
    }
}
