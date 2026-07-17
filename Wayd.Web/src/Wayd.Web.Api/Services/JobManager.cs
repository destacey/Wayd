using Hangfire;
using Wayd.AppIntegration.Application.Interfaces;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Application.Enums;
using Wayd.Common.Application.Exceptions;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Planning.Application.Iterations.Queries;
using Wayd.Planning.Application.PlanningTeams.Commands;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Ranking.Commands;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Ranking.Queries;
using Wayd.ProjectPortfolioManagement.Application.PpmTeams.Commands;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.StrategicManagement.Application.StrategicThemes.Queries;
using Wayd.Web.Api.Interfaces;
using Wayd.Work.Application.WorkIterations.Commands;
using Wayd.Work.Application.WorkProjects.Commands;
using Wayd.Work.Application.WorkTeams.Commands;
using PpmSyncStrategicThemesCommand = Wayd.ProjectPortfolioManagement.Application.StrategicThemes.Commands.SyncStrategicThemesCommand;

namespace Wayd.Web.Api.Services;

public class JobManager(
    ILogger<JobManager> logger,
    IWorkSyncRunner workSyncRunner,
    IPeopleSyncRunner peopleSyncRunner,
    IDispatcher dispatcher)
    : IJobManager
{
    // TODO: does this belong in JobService/HangfireService?

    private readonly ILogger<JobManager> _logger = logger;
    private readonly IWorkSyncRunner _workSyncRunner = workSyncRunner;
    private readonly IPeopleSyncRunner _peopleSyncRunner = peopleSyncRunner;
    private readonly IDispatcher _dispatcher = dispatcher;

    [DisableConcurrentExecution(60 * 3)]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [30, 60, 120])]
    public async Task RunPeopleSync(SyncType syncType, SyncTriggerSource trigger, Guid? connectionId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {BackgroundJob} job (syncType={SyncType}, trigger={Trigger}, connectionId={ConnectionId})", nameof(RunPeopleSync), syncType, trigger, connectionId);

        var result = connectionId.HasValue
            ? await _peopleSyncRunner.Run(connectionId.Value, trigger, syncType, cancellationToken)
            : await _peopleSyncRunner.Run(trigger, syncType, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogError("Failed to run people sync: {Error}", result.Error);
            throw new InternalServerException($"Failed to run people sync. Error: {result.Error}");
        }
        _logger.LogInformation("Completed {BackgroundJob} job", nameof(RunPeopleSync));
    }

    [DisableConcurrentExecution(60 * 3)]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [30, 60, 120])]
    public async Task RunWorkSync(SyncType syncType, SyncTriggerSource trigger, Guid? connectionId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {BackgroundJob} job (trigger={Trigger}, connectionId={ConnectionId})", nameof(RunWorkSync), trigger, connectionId);

        var result = connectionId.HasValue
            ? await _workSyncRunner.Run(connectionId.Value, syncType, trigger, cancellationToken)
            : await _workSyncRunner.Run(syncType, trigger, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogError("Failed to run work sync: {Error}", result.Error);
            throw new InternalServerException($"Failed to run work sync. Error: {result.Error}");
        }
        _logger.LogInformation("Completed {BackgroundJob} job", nameof(RunWorkSync));
    }

    [DisableConcurrentExecution(60 * 3)]
    public async Task RunSyncTeamsWithGraphTables(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {BackgroundJob} job", nameof(RunSyncTeamsWithGraphTables));

        var teamNodesresult = await _dispatcher.Send(new SyncTeamNodesCommand(), cancellationToken);
        if (teamNodesresult.IsFailure)
        {
            _logger.LogError("Failed to sync teams with graph tables: {Error}", teamNodesresult.Error);
        }

        var teamMembershipEdgesResult = await _dispatcher.Send(new SyncTeamMembershipEdgesCommand(), cancellationToken);
        if (teamMembershipEdgesResult.IsFailure)
        {
            _logger.LogError("Failed to sync team memberships with graph tables: {Error}", teamMembershipEdgesResult.Error);
        }

        _logger.LogInformation("Completed {BackgroundJob} job", nameof(RunSyncTeamsWithGraphTables));
    }

    [DisableConcurrentExecution(60 * 3)]
    public async Task RunSyncIterations(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {BackgroundJob} job", nameof(RunSyncIterations));

        var iterations = await _dispatcher.Send(new GetSimpleIterationsQuery(), cancellationToken);

        var result = await _dispatcher.Send(new SyncWorkIterationsCommand(iterations), cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogError("Failed to sync iterations: {Error}", result.Error);
        }

        _logger.LogInformation("Completed {BackgroundJob} job", nameof(RunSyncIterations));
    }


    [DisableConcurrentExecution(60 * 3)]
    public async Task RunSyncStrategicThemes(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {BackgroundJob} job", nameof(RunSyncStrategicThemes));

        var strategicThemes = await _dispatcher.Send(new GetStrategicThemesDataQuery(), cancellationToken);

        var result = await _dispatcher.Send(new PpmSyncStrategicThemesCommand(strategicThemes), cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogError("Failed to sync strategic themes: {Error}", result.Error);
        }

        _logger.LogInformation("Completed {BackgroundJob} job", nameof(RunSyncStrategicThemes));
    }

    [DisableConcurrentExecution(60 * 3)]
    public async Task RunSyncProjects(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {BackgroundJob} job", nameof(RunSyncProjects));

        var projects = await _dispatcher.Send(new GetSimpleProjectsQuery(), cancellationToken);

        var result = await _dispatcher.Send(new SyncWorkProjectsCommand(projects), cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogError("Failed to sync projects: {Error}", result.Error);
        }
        _logger.LogInformation("Completed {BackgroundJob} job", nameof(RunSyncProjects));
    }



    [DisableConcurrentExecution(60 * 3)]
    public async Task RunSyncTeams(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {BackgroundJob} job", nameof(RunSyncTeams));

        var teams = await _dispatcher.Send(new GetSimpleTeamsQuery(), cancellationToken);

        var planningSyncResult = await _dispatcher.Send(new SyncPlanningTeamsCommand(teams), cancellationToken);
        if (planningSyncResult.IsFailure)
        {
            _logger.LogError("Failed to sync planning teams: {Error}", planningSyncResult.Error);
        }

        var ppmSyncResult = await _dispatcher.Send(new SyncPpmTeamsCommand(teams), cancellationToken);
        if (ppmSyncResult.IsFailure)
        {
            _logger.LogError("Failed to sync PPM teams: {Error}", ppmSyncResult.Error);
        }

        var workSyncResult = await _dispatcher.Send(new SyncWorkTeamsCommand(teams), cancellationToken);
        if (workSyncResult.IsFailure)
        {
            _logger.LogError("Failed to sync work teams: {Error}", workSyncResult.Error);
        }

        _logger.LogInformation("Completed {BackgroundJob} job", nameof(RunSyncTeams));
    }

    [DisableConcurrentExecution(60 * 30)]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [60, 300, 600])]
    public async Task RunPortfolioRankRebalance(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {BackgroundJob} job", nameof(RunPortfolioRankRebalance));

        // Runs as the system identity (set by the Hangfire activator), so the rebalance command
        // bypasses the per-actor Owner/Manager check for this maintenance pass.
        var portfolioIds = await _dispatcher.Send(new GetPortfolioIdsToRebalanceQuery(), cancellationToken);

        foreach (var portfolioId in portfolioIds)
        {
            var result = await _dispatcher.Send(new RebalancePortfolioRanksCommand(portfolioId), cancellationToken);
            if (result.IsFailure)
            {
                _logger.LogWarning("Failed to rebalance ranks for portfolio {PortfolioId}: {Error}", portfolioId, result.Error);
            }
        }

        _logger.LogInformation("Completed {BackgroundJob} job ({Count} portfolios)", nameof(RunPortfolioRankRebalance), portfolioIds.Count);
    }
}
