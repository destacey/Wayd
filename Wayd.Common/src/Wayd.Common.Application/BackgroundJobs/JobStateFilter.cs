namespace Wayd.Common.Application.BackgroundJobs;

/// <summary>
/// The job lifecycle states background jobs can be listed by.
/// </summary>
/// <remarks>
/// Wayd's own vocabulary — the scheduler's state names are mapped to these in the
/// infrastructure layer and never reach the API contract.
/// </remarks>
public enum JobStateFilter
{
    Processing = 0,
    Scheduled = 1,
    Enqueued = 2,
    Failed = 3,
    Succeeded = 4,
    Deleted = 5,
}
