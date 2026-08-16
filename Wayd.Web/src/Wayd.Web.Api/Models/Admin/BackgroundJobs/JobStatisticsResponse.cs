using Wayd.Common.Application.BackgroundJobs;

namespace Wayd.Web.Api.Models.Admin.BackgroundJobs;

/// <summary>
/// Job counts, split by what the number measures. <see cref="Current"/> values agree with the
/// matching job list; <see cref="AllTime"/> values are running totals that outlive the job records.
/// </summary>
public class JobStatisticsResponse
{
    public CurrentJobCountsResponse Current { get; set; } = new();
    public AllTimeJobCountsResponse AllTime { get; set; } = new();

    internal static JobStatisticsResponse From(JobStatisticsDto statistics) => new()
    {
        Current = CurrentJobCountsResponse.From(statistics.Current),
        AllTime = AllTimeJobCountsResponse.From(statistics.AllTime),
    };
}

/// <summary>Counts of jobs that exist now. Each agrees with the matching job list.</summary>
public class CurrentJobCountsResponse
{
    public long Enqueued { get; set; }
    public long Scheduled { get; set; }
    public long Processing { get; set; }
    public long Failed { get; set; }
    public long Succeeded { get; set; }
    public long Deleted { get; set; }

    /// <summary>Null when the storage provider does not compute it.</summary>
    public long? Retries { get; set; }

    /// <summary>Null when the storage provider does not compute it.</summary>
    public long? Awaiting { get; set; }

    public long Recurring { get; set; }
    public long Servers { get; set; }

    internal static CurrentJobCountsResponse From(CurrentJobCountsDto counts) => new()
    {
        Enqueued = counts.Enqueued,
        Scheduled = counts.Scheduled,
        Processing = counts.Processing,
        Failed = counts.Failed,
        Succeeded = counts.Succeeded,
        Deleted = counts.Deleted,
        Retries = counts.Retries,
        Awaiting = counts.Awaiting,
        Recurring = counts.Recurring,
        Servers = counts.Servers,
    };
}

/// <summary>Running totals since the job store was created.</summary>
public class AllTimeJobCountsResponse
{
    public long Succeeded { get; set; }
    public long Deleted { get; set; }

    internal static AllTimeJobCountsResponse From(AllTimeJobCountsDto counts) => new()
    {
        Succeeded = counts.Succeeded,
        Deleted = counts.Deleted,
    };
}
