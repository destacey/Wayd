namespace Wayd.Common.Application.BackgroundJobs;

/// <summary>
/// Job counts, split by what the number actually measures.
/// </summary>
/// <remarks>
/// The two groups are not comparable and must not be presented as one set.
/// <see cref="Current"/> counts jobs that exist right now, so each value agrees with the matching
/// job list. <see cref="AllTime"/> values are running totals from the scheduler's counters: they
/// keep climbing after the job records are purged, so they routinely exceed what the corresponding
/// list returns — often by orders of magnitude.
/// </remarks>
public sealed record JobStatisticsDto
{
    public CurrentJobCountsDto Current { get; set; } = new();
    public AllTimeJobCountsDto AllTime { get; set; } = new();
}

/// <summary>Counts of jobs that exist now. Each agrees with the matching job list.</summary>
public sealed record CurrentJobCountsDto
{
    public long Enqueued { get; set; }
    public long Scheduled { get; set; }
    public long Processing { get; set; }
    public long Failed { get; set; }

    /// <summary>Retained succeeded jobs — far fewer than <see cref="AllTimeJobCountsDto.Succeeded"/>, which keeps counting after they are purged.</summary>
    public long Succeeded { get; set; }

    /// <summary>Retained deleted jobs.</summary>
    public long Deleted { get; set; }

    /// <summary>Jobs waiting out a retry cooldown. Null when the storage provider does not compute it.</summary>
    public long? Retries { get; set; }

    /// <summary>Continuations waiting on a parent job. Null when the storage provider does not compute it.</summary>
    public long? Awaiting { get; set; }

    public long Recurring { get; set; }
    public long Servers { get; set; }
}

/// <summary>Running totals since the job store was created. These outlive the job records themselves.</summary>
public sealed record AllTimeJobCountsDto
{
    public long Succeeded { get; set; }
    public long Deleted { get; set; }
}
