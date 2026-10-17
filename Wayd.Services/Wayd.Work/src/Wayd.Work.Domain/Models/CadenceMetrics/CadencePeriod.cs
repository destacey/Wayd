using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Organizations;

namespace Wayd.Work.Domain.Models.CadenceMetrics;

/// <summary>
/// One operating model of a Kanban team that has a reporting cadence: the days it covers, and how the team's
/// days and work were counted then.
/// </summary>
/// <param name="Start">The model's first day.</param>
/// <param name="End">The model's last day, included in it; null for the current model.</param>
/// <param name="TimeZone">The IANA id of the zone the model's days are counted in.</param>
/// <param name="SizingMethod">The estimate the team's work was measured in under this model.</param>
/// <param name="Cadence">The periods the team's velocity was reported in under this model.</param>
public sealed record CadenceSchedulePeriod(LocalDate Start, LocalDate? End, string TimeZone, SizingMethod SizingMethod, ReportingCadence Cadence)
{
    /// <summary>
    /// Whether <paramref name="next"/> starts the day after this ends and counts periods the same way, so a period
    /// runs on across the change: an operating model set for another reason does not cut the open period short.
    /// </summary>
    public bool ContinuesInto(CadenceSchedulePeriod next) =>
        End is { } end
        && next.Start == end.PlusDays(1)
        && next.TimeZone == TimeZone
        && next.SizingMethod == SizingMethod
        && next.Cadence == Cadence;
}

/// <summary>
/// One of a Kanban team's reporting periods: a stretch of its cadence, cut short where the cadence, time zone or
/// sizing method changes partway through it.
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
    /// Each run of operating models that count periods the same way follows its own cadence and stops where the run
    /// does, so a change of cadence, zone or sizing method closes the open period the day before the new model
    /// starts and leaves earlier periods as they were. Days under a model with no cadence belong to no period.
    /// Where consecutive models count days in different zones, the older period ends where the newer one begins,
    /// so no completion falls in both or neither.
    /// </remarks>
    public static IReadOnlyList<CadencePeriod> Recent(IEnumerable<CadenceSchedulePeriod> schedule, Instant now, int window)
    {
        var periods = new List<CadencePeriod>();
        CadencePeriod? newer = null;

        foreach (var model in Runs(schedule).OrderByDescending(m => m.Start))
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
                // The day after this period starts the newer one, perhaps in another zone: meet it exactly.
                if (newer is not null && newer.Start == finish.PlusDays(1))
                    until = newer.From;
                if (until <= from)
                    continue;

                newer = new CadencePeriod(
                    start,
                    finish,
                    model.SizingMethod,
                    IsPartial: start != full.Start || finish != full.End,
                    IsCurrent: until > now,
                    from,
                    until);
                periods.Add(newer);
            }
        }

        return periods;
    }

    /// <summary>The schedule with each run of models that <see cref="CadenceSchedulePeriod.ContinuesInto"/> the next joined into one.</summary>
    private static List<CadenceSchedulePeriod> Runs(IEnumerable<CadenceSchedulePeriod> schedule)
    {
        var runs = new List<CadenceSchedulePeriod>();
        foreach (var model in schedule.OrderBy(m => m.Start))
        {
            if (runs.Count > 0 && runs[^1].ContinuesInto(model))
                runs[^1] = runs[^1] with { End = model.End };
            else
                runs.Add(model);
        }

        return runs;
    }
}
