using CSharpFunctionalExtensions;
using Hangfire;
using Wayd.AppIntegration.Application.Interfaces;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Application.Attachments.Commands;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Enums;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Notifications.Commands;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Application.Requests.Planning.Queries;
using Wayd.Common.Application.Requests.ProjectPortfolioManagement;
using Wayd.DemandManagement.Application.DemandEstimationScales.Commands;
using Wayd.DemandManagement.Application.DemandProjectDeliverables.Commands;
using Wayd.DemandManagement.Application.DemandProjects.Commands;
using Wayd.Infrastructure.BackgroundJobs;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Work.Application.Iterations.Queries;
using Wayd.Planning.Application.PlanningSprints.Commands;
using Wayd.Planning.Application.PlanningTeams.Commands;
using Wayd.ProductManagement.Application.ProductTeams.Commands;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Ranking.Commands;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Ranking.Queries;
using Wayd.ProjectPortfolioManagement.Application.PpmTeams.Commands;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.StrategicManagement.Application.StrategicThemes.Queries;
using Wayd.Web.Api.Interfaces;
using Wayd.Work.Application.WorkProjects.Commands;
using Wayd.Work.Application.WorkTeams.Commands;
using PpmSyncStrategicThemesCommand = Wayd.ProjectPortfolioManagement.Application.StrategicThemes.Commands.SyncStrategicThemesCommand;

namespace Wayd.Web.Api.Services;

public class JobManager(
    ILogger<JobManager> logger,
    IWorkSyncRunner workSyncRunner,
    IPeopleSyncRunner peopleSyncRunner,
    IDispatcher dispatcher,
    IDateTimeProvider dateTimeProvider,
    IUserService userService)
    : IJobManager
{
    // TODO: does this belong in JobService/HangfireService?

    private readonly ILogger<JobManager> _logger = logger;
    private readonly IWorkSyncRunner _workSyncRunner = workSyncRunner;
    private readonly IPeopleSyncRunner _peopleSyncRunner = peopleSyncRunner;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IUserService _userService = userService;

    // No automatic retry. People-sync failures are overwhelmingly deterministic — a duplicate key
    // on a uniquely-indexed employee field, a source returning zero workers. Re-running re-fetches
    // every worker over SOAP, rebuilds the identical change set, and fails on the identical constraint,
    // so the retries burn Workday API quota and emit a ~260 KB exception log apiece without ever
    // changing the outcome. The failure is already durably
    // recorded on the SyncRun row before the throw, so each retry also writes another failed row
    // for one underlying problem.
    //
    // The job still throws and still lands in Hangfire's Failed state — that's the signal the team
    // watches. This only removes the three pointless re-attempts in front of it.
    [SkipConcurrentExecution(typeof(SyncRunCoverage), 60 * 3)]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunPeopleSync(SyncType syncType, SyncTriggerSource trigger, Guid? connectionId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {BackgroundJob} job (syncType={SyncType}, trigger={Trigger}, connectionId={ConnectionId})", nameof(RunPeopleSync), syncType, trigger, connectionId);

        var result = connectionId.HasValue
            ? await _peopleSyncRunner.Run(connectionId.Value, trigger, syncType, cancellationToken)
            : await _peopleSyncRunner.Run(trigger, syncType, cancellationToken);

        ThrowIfFailed(result, "run people sync");

        _logger.LogInformation("Completed {BackgroundJob} job", nameof(RunPeopleSync));
    }

    [SkipConcurrentExecution(typeof(SyncRunCoverage), 60 * 3)]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [30, 60, 120])]
    public async Task RunWorkSync(SyncType syncType, SyncTriggerSource trigger, Guid? connectionId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {BackgroundJob} job (trigger={Trigger}, connectionId={ConnectionId})", nameof(RunWorkSync), trigger, connectionId);

        var result = connectionId.HasValue
            ? await _workSyncRunner.Run(connectionId.Value, syncType, trigger, cancellationToken)
            : await _workSyncRunner.Run(syncType, trigger, cancellationToken);

        ThrowIfFailed(result, "run work sync");

        _logger.LogInformation("Completed {BackgroundJob} job", nameof(RunWorkSync));
    }

    // The five replication syncs that follow (iterations, strategic themes, projects, estimation scales,
    // teams) do not retry. An admin runs them by hand and watches for the outcome, and their failures are
    // data problems — a sprint whose team has no copy yet, a theme a project still tags — that a re-run
    // meets again. Hangfire's default ten retries would hold the job in Scheduled for hours before it
    // showed as Failed.
    [SkipConcurrentExecution]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunSyncIterations(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {BackgroundJob} job", nameof(RunSyncIterations));

        // Taken before the read: a copy's watermark must never claim a change the read could not have seen.
        var asOf = _dateTimeProvider.Now;
        var iterations = await _dispatcher.Send(new GetSimpleIterationsQuery(), cancellationToken);

        var result = await _dispatcher.Send(new SyncPlanningSprintsCommand(iterations, asOf), cancellationToken);
        ThrowIfFailed(result, "sync Planning sprints");

        _logger.LogInformation("Completed {BackgroundJob} job", nameof(RunSyncIterations));
    }

    [SkipConcurrentExecution]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunSyncStrategicThemes(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {BackgroundJob} job", nameof(RunSyncStrategicThemes));

        var asOf = _dateTimeProvider.Now;
        var strategicThemes = await _dispatcher.Send(new GetStrategicThemesDataQuery(), cancellationToken);

        var result = await _dispatcher.Send(new PpmSyncStrategicThemesCommand(strategicThemes, asOf), cancellationToken);
        ThrowIfFailed(result, "sync strategic themes");

        _logger.LogInformation("Completed {BackgroundJob} job", nameof(RunSyncStrategicThemes));
    }

    [SkipConcurrentExecution]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunSyncProjects(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {BackgroundJob} job", nameof(RunSyncProjects));

        var asOf = _dateTimeProvider.Now;
        var projects = await _dispatcher.Send(new GetSimpleProjectsQuery(), cancellationToken);

        // Each copy is synced on its own, so one module's failure never leaves the other stale; the job
        // still fails once they have all run. The deliverables go last: each deliverable copy needs its
        // project's copy.
        ThrowIfAnyFailed("Failed to sync one or more project copies.",
        [
            await RunIsolatedStep("sync Work projects", () => _dispatcher.Send(new SyncWorkProjectsCommand(projects, asOf), cancellationToken)),
            await RunIsolatedStep("sync Demand Management projects", () => _dispatcher.Send(new SyncDemandProjectsCommand(projects, asOf), cancellationToken)),
            await RunIsolatedStep("sync Demand Management project deliverables", async () =>
            {
                var deliverablesAsOf = _dateTimeProvider.Now;
                var deliverables = await _dispatcher.Send(new GetSimpleProjectDeliverablesQuery(), cancellationToken);
                return await _dispatcher.Send(new SyncDemandProjectDeliverablesCommand(deliverables, deliverablesAsOf), cancellationToken);
            }),
        ]);

        _logger.LogInformation("Completed {BackgroundJob} job", nameof(RunSyncProjects));
    }

    [SkipConcurrentExecution]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunSyncEstimationScales(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {BackgroundJob} job", nameof(RunSyncEstimationScales));

        var asOf = _dateTimeProvider.Now;
        var scales = await _dispatcher.Send(new GetEstimationScaleSnapshotsQuery(), cancellationToken);

        var result = await _dispatcher.Send(new SyncDemandEstimationScalesCommand(scales, asOf), cancellationToken);
        ThrowIfFailed(result, "sync Demand Management estimation scales");

        _logger.LogInformation("Completed {BackgroundJob} job", nameof(RunSyncEstimationScales));
    }

    [SkipConcurrentExecution]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunSyncTeams(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {BackgroundJob} job", nameof(RunSyncTeams));

        var asOf = _dateTimeProvider.Now;
        var teams = await _dispatcher.Send(new GetSimpleTeamsQuery(), cancellationToken);

        // Each copy is synced on its own, so one module's failure never leaves the others stale; the job
        // still fails once they have all run.
        ThrowIfAnyFailed("Failed to sync one or more modules' team copies.",
        [
            await RunIsolatedStep("sync planning teams", () => _dispatcher.Send(new SyncPlanningTeamsCommand(teams, asOf), cancellationToken)),
            await RunIsolatedStep("sync PPM teams", () => _dispatcher.Send(new SyncPpmTeamsCommand(teams, asOf), cancellationToken)),
            await RunIsolatedStep("sync product teams", () => _dispatcher.Send(new SyncProductTeamsCommand(teams, asOf), cancellationToken)),
            await RunIsolatedStep("sync work teams", () => _dispatcher.Send(new SyncWorkTeamsCommand(teams, asOf), cancellationToken)),
        ]);

        _logger.LogInformation("Completed {BackgroundJob} job", nameof(RunSyncTeams));
    }

    // Retried, unlike the replication syncs: it is queued by an identity mapping rather than run by an admin
    // who would run it again, its one set-based update can collide with a work sync writing the same rows,
    // and repeating it is safe. It waits for a running repoint rather than being skipped: a queued run carries an
    // identity and a decision of its own, which the running one does not cover.
    [DisableConcurrentExecution(60 * 3)]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [30, 60, 120])]
    public async Task RunRepointWorkItemAttribution(string externalId, Guid? employeeId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {BackgroundJob} job for external identity {ExternalId}",
            nameof(RunRepointWorkItemAttribution), externalId);

        var result = await _dispatcher.Send(
            new RepointWorkItemAttributionCommand(externalId, employeeId), cancellationToken);
        ThrowIfFailed(result, $"repoint work item attribution for {externalId}");

        _logger.LogInformation("Completed {BackgroundJob} job for external identity {ExternalId}",
            nameof(RunRepointWorkItemAttribution), externalId);
    }

    // Retried: a rebalance only re-spaces ranks, so repeating it is safe, and a retry touches only the
    // portfolios still dense. Ranking takes no concurrency token, so a failure is most likely a transient
    // database error.
    [SkipConcurrentExecution]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [60, 300, 600])]
    public async Task RunPortfolioRankRebalance(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {BackgroundJob} job", nameof(RunPortfolioRankRebalance));

        // Runs as the system identity (set by the Hangfire activator), so the rebalance command
        // bypasses the per-actor Owner/Manager check for this maintenance pass.
        var portfolioIds = await _dispatcher.Send(new GetPortfolioIdsToRebalanceQuery(), cancellationToken);

        // Each portfolio is rebalanced on its own, so one's failure never leaves the others dense; the job
        // still fails once they have all run.
        List<Exception?> failures = [];
        foreach (var portfolioId in portfolioIds)
        {
            failures.Add(await RunIsolatedStep($"rebalance ranks for portfolio {portfolioId}",
                () => _dispatcher.Send(new RebalancePortfolioRanksCommand(portfolioId), cancellationToken)));
        }
        ThrowIfAnyFailed("Failed to rebalance ranks for one or more portfolios.", [.. failures]);

        _logger.LogInformation("Completed {BackgroundJob} job ({Count} portfolios)", nameof(RunPortfolioRankRebalance), portfolioIds.Count);
    }

    // No retry: the sweep is idempotent and runs again on its own schedule, so a failed pass is left for
    // the next one. It still fails, so the miss shows.
    [SkipConcurrentExecution]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunImportStallRecovery(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {BackgroundJob} job", nameof(RunImportStallRecovery));

        var result = await _dispatcher.Send(new RecoverStalledImportsCommand(), cancellationToken);
        ThrowIfFailed(result, "recover stalled imports");

        _logger.LogInformation(
            "Completed {BackgroundJob} job ({Republished} requeued, {Failed} failed)",
            nameof(RunImportStallRecovery), result.Value.Republished, result.Value.Failed);
    }

    [SkipConcurrentExecution]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunImportRetentionSweep(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {BackgroundJob} job", nameof(RunImportRetentionSweep));

        var result = await _dispatcher.Send(new PurgeExpiredImportPayloadsCommand(), cancellationToken);
        ThrowIfFailed(result, "sweep expired import payloads");

        _logger.LogInformation(
            "Completed {BackgroundJob} job ({Count} row payloads cleared)", nameof(RunImportRetentionSweep), result.Value);
    }

    [SkipConcurrentExecution]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunNotificationRetentionSweep(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {BackgroundJob} job", nameof(RunNotificationRetentionSweep));

        var result = await _dispatcher.Send(new PurgeExpiredNotificationsCommand(), cancellationToken);
        ThrowIfFailed(result, "sweep expired notifications");

        _logger.LogInformation(
            "Completed {BackgroundJob} job ({Count} notifications deleted)", nameof(RunNotificationRetentionSweep), result.Value);
    }

    // No retry: it runs again within the hour, and an employee left active for one more pass is the cost.
    [SkipConcurrentExecution]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunEmployeeAccessEnd(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {BackgroundJob} job", nameof(RunEmployeeAccessEnd));

        var result = await _dispatcher.Send(new DeactivateEmployeesPastAccessEndCommand(), cancellationToken);
        ThrowIfFailed(result, "deactivate employees past their access end date");

        // The employee's deactivation reaches its linked user only through the user sync.
        if (result.Value > 0)
        {
            var usersResult = await _userService.SyncUsersFromEmployeeRecords(cancellationToken);
            ThrowIfFailed(usersResult, "deactivate the users linked to them");
        }

        _logger.LogInformation(
            "Completed {BackgroundJob} job ({Count} employees deactivated)", nameof(RunEmployeeAccessEnd), result.Value);
    }

    [SkipConcurrentExecution]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAttachmentSweep(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {BackgroundJob} job", nameof(RunAttachmentSweep));

        var result = await _dispatcher.Send(new PurgeAbandonedAttachmentsCommand(), cancellationToken);
        ThrowIfFailed(result, "sweep abandoned attachments");

        _logger.LogInformation(
            "Completed {BackgroundJob} job ({Count} attachments deleted)", nameof(RunAttachmentSweep), result.Value);
    }

    /// <summary>
    /// Runs one step of a job whose steps are independent, returning its failure (a failed result as an
    /// exception) rather than throwing it, so the job's later steps still run. Cancellation is not a
    /// failure and propagates.
    /// </summary>
    private async Task<Exception?> RunIsolatedStep(string step, Func<Task<Result>> run)
    {
        try
        {
            var result = await run();
            if (result.IsSuccess)
                return null;

            _logger.LogError("Failed to {Step}: {Error}", step, result.Error);
            return new InternalServerException($"Failed to {step}. Error: {result.Error}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to {Step}", step);
            return ex;
        }
    }

    /// <summary>Fails the running job once all its isolated steps have run, carrying every step's failure.</summary>
    private static void ThrowIfAnyFailed(string message, Exception?[] stepFailures)
    {
        var failed = stepFailures.OfType<Exception>().ToList();
        if (failed.Count != 0)
            throw new AggregateException(message, failed);
    }

    /// <summary>Fails the running job with a step's failed result, so Hangfire records it as Failed rather than Succeeded.</summary>
    private void ThrowIfFailed(Result result, string step)
    {
        if (result.IsSuccess)
            return;

        _logger.LogError("Failed to {Step}: {Error}", step, result.Error);
        throw new InternalServerException($"Failed to {step}. Error: {result.Error}");
    }
}
