using Wayd.Common.Application.BackgroundJobs;

namespace Wayd.Web.Api.Models.Admin.BackgroundJobs;

/// <summary>A page of jobs in one lifecycle state. <see cref="PageNumber"/> is 0-based.</summary>
public class JobsResponse
{
    public long TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public List<JobSummaryResponse> Items { get; set; } = [];

    internal static JobsResponse From(JobPageDto page) => new()
    {
        TotalCount = page.TotalCount,
        PageNumber = page.PageNumber,
        PageSize = page.PageSize,
        Items = [.. page.Items.Select(JobSummaryResponse.From)],
    };
}
