using Hangfire;
using Wayd.AppIntegration.Application.Interfaces;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Application.Enums;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Interfaces;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Planning.Application.Iterations.Queries;
using Wayd.Planning.Application.PlanningTeams.Commands;
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
    IEmployeeService employeeService,
    IWorkSyncRunner workSyncRunner,
    ISender sender)
    : IJobManager
{
    // TODO: does this belong in JobService/HangfireService?

    private readonly ILogger<JobManager> _logger = logger;
    private readonly IEmployeeService _employeeService = employeeService;
    private readonly IWorkSyncRunner _workSyncRunner = workSyncRunner;
    private readonly ISender _sender = sender;

    [DisableConcurrentExecution(60)]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [30, 60, 120])]
    public async Task RunSyncExternalEmployees(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running {BackgroundJob} job", nameof(RunSyncExternalEmployees));
        var result = await _employeeService.SyncExternalEmployees(cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogError("Failed to sync external employees: {Error}", result.Error);
            throw new InternalServerException($"Failed to sync external employees. Error: {result.Error}");
        }
        _logger.LogInformation("Completed {BackgroundJob} job", nameof(RunSyncExternalEmployees));
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

        var teamNodesresult = await _sender.Send(new SyncTeamNodesCommand(), cancellationToken);
        if (teamNodesresult.IsFailure)
        {
            _logger.LogError("Failed to sync teams with graph tables: {Error}", teamNodesresult.Error);
        }

        var teamMembershipEdgesResult = await _sender.Send(new SyncTeamMembershipEdgesCommand(), cancellationToken);
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

        var iterations = await _sender.Send(new GetSimpleIterationsQuery(), cancellationToken);

        var result = await _sender.Send(new SyncWorkIterationsCommand(iterations), cancellationToken);
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

        var strategicThemes = await _sender.Send(new GetStrategicThemesDataQuery(), cancellationToken);

        var result = await _sender.Send(new PpmSyncStrategicThemesCommand(strategicThemes), cancellationToken);
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

        var projects = await _sender.Send(new GetSimpleProjectsQuery(), cancellationToken);

        var result = await _sender.Send(new SyncWorkProjectsCommand(projects), cancellationToken);
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

        var teams = await _sender.Send(new GetSimpleTeamsQuery(), cancellationToken);

        var planningSyncResult = await _sender.Send(new SyncPlanningTeamsCommand(teams), cancellationToken);
        if (planningSyncResult.IsFailure)
        {
            _logger.LogError("Failed to sync planning teams: {Error}", planningSyncResult.Error);
        }

        var ppmSyncResult = await _sender.Send(new SyncPpmTeamsCommand(teams), cancellationToken);
        if (ppmSyncResult.IsFailure)
        {
            _logger.LogError("Failed to sync PPM teams: {Error}", ppmSyncResult.Error);
        }

        var workSyncResult = await _sender.Send(new SyncWorkTeamsCommand(teams), cancellationToken);
        if (workSyncResult.IsFailure)
        {
            _logger.LogError("Failed to sync work teams: {Error}", workSyncResult.Error);
        }

        _logger.LogInformation("Completed {BackgroundJob} job", nameof(RunSyncTeams));
    }
}
