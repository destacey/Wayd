using Wayd.Common.Application.BackgroundJobs;

namespace Wayd.Web.Api.Models.Admin.BackgroundJobs;

/// <summary>Counts per lifecycle bucket, for the dashboard tiles.</summary>
public class JobStatisticsResponse
{
    public long Enqueued { get; set; }
    public long Scheduled { get; set; }
    public long Processing { get; set; }
    public long Succeeded { get; set; }
    public long Failed { get; set; }
    public long Deleted { get; set; }
    public long Recurring { get; set; }
    public long Servers { get; set; }

    internal static JobStatisticsResponse From(JobStatisticsDto statistics) => new()
    {
        Enqueued = statistics.Enqueued,
        Scheduled = statistics.Scheduled,
        Processing = statistics.Processing,
        Succeeded = statistics.Succeeded,
        Failed = statistics.Failed,
        Deleted = statistics.Deleted,
        Recurring = statistics.Recurring,
        Servers = statistics.Servers,
    };
}
