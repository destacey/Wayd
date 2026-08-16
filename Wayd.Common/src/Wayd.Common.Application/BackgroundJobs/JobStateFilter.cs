namespace Wayd.Common.Application.BackgroundJobs;

/// <summary>
/// The job lifecycle buckets that can be listed. Wayd's own vocabulary — the scheduler's state
/// names are mapped to these in the infrastructure layer and never reach the API contract.
/// </summary>
public enum JobStateFilter
{
    Processing = 0,
    Scheduled = 1,
    Enqueued = 2,
    Failed = 3,
    Succeeded = 4,
    Deleted = 5,
}
