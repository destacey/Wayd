using Hangfire;
using Wayd.Common.Application.BackgroundJobs;
using Wayd.Web.Api.Interfaces;

namespace Wayd.Web.Api.Services;

/// <summary>
/// Schedules the maintenance a deployment depends on, so that it runs without an admin having to know it
/// exists. Today that is the import stall sweep: the only thing that ends a run whose worker died before it
/// could record the failure, and the only thing that republishes a run whose message never arrived.
/// </summary>
/// <remarks>
/// Added only when absent, and matched on the method rather than an id, so a schedule an admin already made
/// under a name of their own keeps its cron and is not joined by a second. Deleting it only lasts until the
/// next start.
/// </remarks>
public static class DefaultRecurringJobs
{
    public const string ImportStallRecoveryJobId = "import-stall-recovery";

    // Its grace periods are 15 and 30 minutes, so checking every five keeps the wait close to them.
    private const string ImportStallRecoveryCron = "*/5 * * * *";

    public static void EnsureDefaultRecurringJobs(this IServiceProvider services)
    {
        // Resolving the storage is what initializes JobStorage.Current, which the job service reads.
        services.GetRequiredService<JobStorage>();

        using var scope = services.CreateScope();
        var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();
        var jobManager = scope.ServiceProvider.GetRequiredService<IJobManager>();

        var alreadyScheduled = jobService.GetRecurringJobs()
            .Any(j => j.Action == nameof(IJobManager.RunImportStallRecovery));

        if (alreadyScheduled)
            return;

        jobService.AddOrUpdate(
            ImportStallRecoveryJobId,
            () => jobManager.RunImportStallRecovery(CancellationToken.None),
            () => ImportStallRecoveryCron);
    }
}
