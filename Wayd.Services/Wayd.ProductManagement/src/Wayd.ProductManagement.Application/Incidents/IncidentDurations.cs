using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.ProductManagement.Application.Incidents;

/// <summary>
/// The stretches an incident can already answer from when it was noticed, when response started,
/// and when it recovered.
/// </summary>
internal static class IncidentDurations
{
    /// <summary>The dates and status a duration reads. Nothing here is inferred from a status name.</summary>
    internal readonly record struct Row(
        int Key,
        string Title,
        Instant NoticedAt,
        Instant? StartedAt,
        Instant? RecoveredAt,
        ProductStatusAlias StatusAlias,
        StatusCategory StatusCategory);

    /// <summary>One incident's three stretches. Null means that stretch has not started.</summary>
    internal readonly record struct Stretches(
        Duration? NoticedToResponseStarted,
        Duration? ResponseToRecovered,
        Duration? Active);

    /// <summary>The three figures on the incidents grid, for one set of incidents.</summary>
    internal readonly record struct Figures(
        int ActiveCount,
        LongestActive? LongestActive,
        Duration? MeanTimeToRecover);

    /// <summary>The active incident that has been open longest, and how long that is.</summary>
    internal readonly record struct LongestActive(int Key, string Title, Duration Active);

    /// <summary>Measures one incident. Canceled is not given a recovery duration.</summary>
    internal static Stretches Measure(Row row, Instant now)
    {
        var noticedToResponse = row.StartedAt is Instant started
            ? started - row.NoticedAt
            : (Duration?)null;

        var canceled = row.StatusAlias == ProductStatusAlias.Canceled;
        var responseToRecovered = !canceled
            && row.StartedAt is Instant responseStarted
            && row.RecoveredAt is Instant recovered
            ? recovered - responseStarted
            : (Duration?)null;

        Duration? active = null;
        if (IncidentActivity.IsActive(row.StatusAlias, row.StatusCategory))
        {
            active = now - row.NoticedAt;
        }
        else if (!canceled && row.RecoveredAt is Instant recoveredAt)
        {
            active = recoveredAt - row.NoticedAt;
        }

        return new Stretches(noticedToResponse, responseToRecovered, active);
    }

    /// <summary>
    /// Counts active incidents, finds the longest of those from noticed until <paramref name="now"/>,
    /// and averages noticed-to-recovered for closed incidents. Open and canceled incidents are excluded
    /// from that mean. A tie on the longest duration keeps the lower key.
    /// </summary>
    internal static Figures Summarize(IEnumerable<Row> rows, Instant now)
    {
        var list = rows as IReadOnlyList<Row> ?? rows.ToList();
        LongestActive? longest = null;
        var activeCount = 0;

        foreach (var row in list)
        {
            if (!IncidentActivity.IsActive(row.StatusAlias, row.StatusCategory))
            {
                continue;
            }

            activeCount++;
            var active = now - row.NoticedAt;
            if (longest is null
                || active > longest.Value.Active
                || (active == longest.Value.Active && row.Key < longest.Value.Key))
            {
                longest = new LongestActive(row.Key, row.Title, active);
            }
        }

        long totalTicks = 0;
        var closed = 0;
        foreach (var row in list)
        {
            if (row.StatusAlias != ProductStatusAlias.Closed || row.RecoveredAt is not Instant recovered)
            {
                continue;
            }

            totalTicks += (recovered - row.NoticedAt).BclCompatibleTicks;
            closed++;
        }

        var mean = closed == 0
            ? (Duration?)null
            : Duration.FromTimeSpan(TimeSpan.FromTicks(totalTicks / closed));

        return new Figures(activeCount, longest, mean);
    }

    /// <summary>Whole milliseconds, truncating a fraction. Null stays null, which is not zero.</summary>
    internal static long? Milliseconds(Duration? duration) =>
        duration is { } value ? value.BclCompatibleTicks / TimeSpan.TicksPerMillisecond : null;
}
