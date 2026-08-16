namespace Wayd.Common.Application.BackgroundJobs;

/// <summary>
/// The job types that may be registered as recurring (cron) jobs.
///
/// Every type can be run on demand; only these can be scheduled. The set is deliberately narrower
/// than <see cref="BackgroundJobType"/> — the data-replication syncs are triggered by the flows that
/// change the underlying data, not on a clock.
/// </summary>
/// <remarks>
/// This is the single source of truth: the recurring-job endpoint's expression switch and the
/// <see cref="BackgroundJobTypeDto.IsSchedulable"/> flag the UI filters on must both derive from it.
/// Two independent lists would drift, and the failure mode is a job type the form offers but the API
/// rejects.
/// </remarks>
public static class SchedulableBackgroundJobTypes
{
    private static readonly HashSet<BackgroundJobType> _types =
    [
        BackgroundJobType.PeopleFullSync,
        BackgroundJobType.PeopleDiffSync,
        BackgroundJobType.WorkFullSync,
        BackgroundJobType.WorkDiffSync,
        BackgroundJobType.TeamGraphSync,
        BackgroundJobType.PortfolioRankRebalance,
    ];

    public static bool Contains(BackgroundJobType jobType) => _types.Contains(jobType);
}
