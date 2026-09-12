using System.Linq.Expressions;
using Hangfire;
using Wayd.Common.Application.BackgroundJobs;
using Wayd.Web.Api.Interfaces;

namespace Wayd.Web.Api.Services;

/// <summary>
/// Schedules the maintenance a deployment depends on, so that it runs without an admin having to know it
/// exists. Today that is the import stall sweep — the only thing that ends a run whose worker died before
/// it could record the failure, and the only thing that republishes a run whose message never arrived —
/// and the import retention sweep, the only thing that discards the copy of each uploaded file once its
/// run is old enough that nobody will retry it.
/// </summary>
/// <remarks>
/// Each is added only when absent, and matched on the method rather than an id, so a schedule an admin
/// already made under a name of their own keeps its cron and is not joined by a second. Deleting one only
/// lasts until the next start.
/// </remarks>
public static class DefaultRecurringJobs
{
    public const string ImportStallRecoveryJobId = "import-stall-recovery";
    public const string ImportRetentionSweepJobId = "import-retention-sweep";

    // Its grace periods are 15 and 30 minutes, so checking every five keeps the wait close to them.
    private const string ImportStallRecoveryCron = "*/5 * * * *";

    // The window is 30 days, so a payload cleared a day late is nothing; once a night, off the working
    // day, is plenty.
    private const string ImportRetentionSweepCron = "0 3 * * *";

    private static readonly (string JobId, string Action, string Cron, Func<IJobManager, Expression<Func<Task>>> MethodCall)[] _defaults =
    [
        (ImportStallRecoveryJobId, nameof(IJobManager.RunImportStallRecovery), ImportStallRecoveryCron,
            jobManager => () => jobManager.RunImportStallRecovery(CancellationToken.None)),
        (ImportRetentionSweepJobId, nameof(IJobManager.RunImportRetentionSweep), ImportRetentionSweepCron,
            jobManager => () => jobManager.RunImportRetentionSweep(CancellationToken.None)),
    ];

    public static void EnsureDefaultRecurringJobs(this IServiceProvider services)
    {
        // Resolving the storage is what initializes JobStorage.Current, which the job service reads.
        services.GetRequiredService<JobStorage>();

        using var scope = services.CreateScope();
        var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();
        var jobManager = scope.ServiceProvider.GetRequiredService<IJobManager>();

        var scheduledActions = jobService.GetRecurringJobs()
            .Select(j => j.Action)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (jobId, action, cron, methodCall) in _defaults)
        {
            if (scheduledActions.Contains(action))
                continue;

            jobService.AddOrUpdate(jobId, methodCall(jobManager), () => cron);
        }
    }
}
