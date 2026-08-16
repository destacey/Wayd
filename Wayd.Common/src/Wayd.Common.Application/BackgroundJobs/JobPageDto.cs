namespace Wayd.Common.Application.BackgroundJobs;

/// <summary>A page of jobs in one lifecycle state. <see cref="PageNumber"/> is 0-based.</summary>
public sealed record JobPageDto
{
    public long TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public IReadOnlyList<JobSummaryDto> Items { get; set; } = [];
}
