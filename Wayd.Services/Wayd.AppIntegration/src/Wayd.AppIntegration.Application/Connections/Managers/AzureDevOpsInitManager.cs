using Wayd.AppIntegration.Application.Connections.Commands;
using Wayd.AppIntegration.Application.Connections.Commands.AzureDevOps;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureDevOps;
using Wayd.AppIntegration.Application.Connections.Queries.AzureDevOps;
using Wayd.AppIntegration.Application.Interfaces;
using Wayd.AppIntegration.Application.Logging;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Application.Requests.WorkManagement.Dtos;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.Models;
using Wayd.Common.Models;

namespace Wayd.AppIntegration.Application.Connections.Managers;

public sealed class AzureDevOpsInitManager(ILogger<AzureDevOpsInitManager> logger, IAzureDevOpsService azureDevOpsService, IDispatcher dispatcher) : IAzureDevOpsInitManager
{
    private readonly ILogger<AzureDevOpsInitManager> _logger = logger;
    private readonly IAzureDevOpsService _azureDevOpsService = azureDevOpsService;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result> SyncOrganizationConfiguration(Guid connectionId, CancellationToken cancellationToken, Guid? syncId = null)
    {
        try
        {
            var connection = await _dispatcher.Send(new GetAzureDevOpsConnectionQuery(connectionId), cancellationToken);
            if (connection is null)
            {
                _logger.LogError("Unable to find Azure DevOps connection with id {ConnectionId}.", connectionId);
                return Result.Failure($"Unable to find Azure DevOps connection with id {connectionId}.");
            }

            using (_logger.BeginScope(new Dictionary<string, object> { ["ConnectionId"] = connectionId }))
            {
                if (connection.IsValidConfiguration is false)
                {
                    _logger.LogError("The configuration for Azure DevOps connection {ConnectionId} is not valid.", connectionId);
                    return Result.Failure($"The configuration for connection {connectionId} is not valid.");
                }

                var azdoConnection = new AzureDevOpsConnectionContext(connection.Configuration.OrganizationUrl, connection.Configuration.PersonalAccessToken);

                var testConnectionResult = await ExternalCallMeasure.MeasureAsync(_logger, "Azdo_SyncOrganizationConfiguration_TestConnection", () => _azureDevOpsService.TestConnection(azdoConnection), syncId);
                if (testConnectionResult.IsFailure)
                {
                    _logger.LogError("Unable to connect to Azure DevOps for connection {ConnectionId}. {Error}", connectionId, testConnectionResult.Error);
                    return Result.Failure($"Unable to connect to Azure DevOps for connection {connectionId}. {testConnectionResult.Error}");
                }

                // Load Processes
                var workProcessesResult = await ExternalCallMeasure.MeasureAsync(_logger, "Azdo_SyncOrganizationConfiguration_GetWorkProcesses", () => _azureDevOpsService.GetWorkProcesses(azdoConnection, cancellationToken), syncId);
                if (workProcessesResult.IsFailure)
                    return workProcessesResult;

                List<AzureDevOpsBoardsWorkProcess> processes = [];
                foreach (var externalProcess in workProcessesResult.Value)
                {
                    var process = AzureDevOpsBoardsWorkProcess.Create(externalProcess.Id, externalProcess.Name, externalProcess.Description);
                    processes.Add(process);
                }

                // Load Workspaces
                var workspacesResult = await ExternalCallMeasure.MeasureAsync(_logger, "Azdo_SyncOrganizationConfiguration_GetWorkspaces", () => _azureDevOpsService.GetWorkspaces(azdoConnection, cancellationToken), syncId);
                if (workspacesResult.IsFailure)
                    return workspacesResult;

                // Build a lookup from workspace ID to its owning work process ID
                var workspaceToProcessLookup = new Dictionary<Guid, Guid>();
                foreach (var process in workProcessesResult.Value)
                {
                    foreach (var wsId in process.WorkspaceIds)
                    {
                        workspaceToProcessLookup[wsId] = process.Id;
                    }
                }

                List<AzureDevOpsBoardsWorkspace> workspaces = [];
                foreach (var externalWorkspace in workspacesResult.Value)
                {
                    workspaceToProcessLookup.TryGetValue(externalWorkspace.Id, out var workProcessId);
                    var workspace = AzureDevOpsBoardsWorkspace.Create(externalWorkspace.Id, externalWorkspace.Name, externalWorkspace.Description, workProcessId == default ? null : workProcessId);
                    workspaces.Add(workspace);
                }

                var existingWorkProcessIntegrationStates = await _dispatcher.Send(new GetIntegrationRegistrationsForWorkProcessesQuery(), cancellationToken);

                // Load Teams
                List<IExternalTeam> teams = [];
                if (workspaces.Count != 0)
                {
                    var projectIds = workspaces.Select(w => w.ExternalId).ToArray();
                    var teamsResult = await ExternalCallMeasure.MeasureAsync(_logger, "Azdo_SyncOrganizationConfiguration_GetTeams", () => _azureDevOpsService.GetTeams(azdoConnection, projectIds, cancellationToken), syncId);
                    if (teamsResult.IsFailure)
                        return teamsResult;

                    teams = teamsResult.Value;
                }

                var syncConfigurationResult = await _dispatcher.Send(new SyncAzureDevOpsConnectionConfigurationCommand(connectionId, processes, workspaces, existingWorkProcessIntegrationStates, teams), cancellationToken);

                return syncConfigurationResult;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while trying to import projects from Azure DevOps for connection {ConnectionId}.", connectionId);
            return Result.Failure($"An error occurred while trying to import projects from Azure DevOps for connection {connectionId}.");
        }
    }

    public async Task<Result<Guid>> InitWorkProcessIntegration(Guid connectionId, Guid workProcessExternalId, CancellationToken cancellationToken)
    {
        try
        {
            using (_logger.BeginScope(new Dictionary<string, object> { ["ConnectionId"] = connectionId, ["WorkProcessExternalId"] = workProcessExternalId }))
            {
                var connectionCheckResult = await GetValidConnectionDetailsForWorkProcess(connectionId, workProcessExternalId, cancellationToken);
                if (connectionCheckResult.IsFailure)
                    return connectionCheckResult.ConvertFailure<Guid>();

                // Check if THIS connection already has this process integrated
                var connectionWorkProcess = connectionCheckResult.Value.Configuration.WorkProcesses
                    .FirstOrDefault(wp => wp.ExternalId == workProcessExternalId);
                if (connectionWorkProcess?.IntegrationState is not null)
                {
                    _logger.LogError("Unable to initialize a work process {WorkProcessExternalId} from Azure DevOps for connection {ConnectionId} because it is already integrated on this connection.", workProcessExternalId, connectionId);
                    return Result.Failure<Guid>($"Unable to initialize a work process {workProcessExternalId} from Azure DevOps for connection {connectionId} because it is already integrated.");
                }

                // Check if another connection has already created a WorkProcess for this ExternalId.
                // Azure DevOps built-in processes (Agile, Scrum, CMMI, Basic) share the same GUID across
                // all organizations, so we reuse the existing WorkProcess rather than creating a duplicate.
                var existsGlobally = await _dispatcher.Send(new ExternalWorkProcessExistsQuery(workProcessExternalId), cancellationToken);
                if (existsGlobally)
                {
                    var existingRegistrations = await _dispatcher.Send(new GetIntegrationRegistrationsForWorkProcessesQuery(workProcessExternalId), cancellationToken);
                    var existingRegistration = existingRegistrations.FirstOrDefault();
                    if (existingRegistration is null)
                    {
                        _logger.LogError("Work process {WorkProcessExternalId} exists but no integration registration was found.", workProcessExternalId);
                        return Result.Failure<Guid>($"Work process {workProcessExternalId} exists but no integration registration was found.");
                    }

                    _logger.LogInformation("Reusing existing work process {WorkProcessInternalId} for Azure DevOps process {WorkProcessExternalId} on connection {ConnectionId}.", existingRegistration.IntegrationState.InternalId, workProcessExternalId, connectionId);

                    var linkResult = await _dispatcher.Send(new UpdateAzureDevOpsWorkProcessIntegrationStateCommand(connectionId, existingRegistration), cancellationToken);

                    return linkResult.IsSuccess
                        ? Result.Success(existingRegistration.IntegrationState.InternalId)
                        : linkResult.ConvertFailure<Guid>();
                }

                var importWorkspacesResult = await SyncOrganizationConfiguration(connectionId, cancellationToken);
                if (importWorkspacesResult.IsFailure)
                    return importWorkspacesResult.ConvertFailure<Guid>();

                var connectionResult = await GetValidConnectionDetailsForWorkProcess(connectionId, workProcessExternalId, cancellationToken);
                if (connectionResult.IsFailure)
                    return connectionResult.ConvertFailure<Guid>();

                var azdoConnection = new AzureDevOpsConnectionContext(connectionResult.Value.Configuration.OrganizationUrl, connectionResult.Value.Configuration.PersonalAccessToken);

                var processResult = await ExternalCallMeasure.MeasureAsync(_logger, "InitWorkProcessIntegration_GetWorkProcess", () => _azureDevOpsService.GetWorkProcess(azdoConnection, workProcessExternalId, cancellationToken));
                if (processResult.IsFailure)
                    return processResult.ConvertFailure<Guid>();

                var workTypes = processResult.Value.WorkTypes.OfType<IExternalWorkType>().ToList();
                if (workTypes.Count != 0)
                {
                    var levels = await _dispatcher.Send(new GetWorkTypeLevelsQuery(), cancellationToken);
                    if (levels is null)
                        return Result.Failure<Guid>("Unable to get work type levels.");

                    int defaultLevelId = levels.Where(l => l.Tier.Id == (int)WorkTypeTier.Other).Select(l => l.Id).SingleOrDefault();

                    var syncWorkTypesResult = await _dispatcher.Send(new SyncExternalWorkTypesCommand(workTypes, defaultLevelId), cancellationToken);
                    if (syncWorkTypesResult.IsFailure)
                        return syncWorkTypesResult.ConvertFailure<Guid>();
                }

                if (processResult.Value.WorkStatuses.Count != 0)
                {
                    var syncWorkStatusesResult = await _dispatcher.Send(new SyncExternalWorkStatusesCommand(processResult.Value.WorkStatuses), cancellationToken);
                    if (syncWorkStatusesResult.IsFailure)
                        return syncWorkStatusesResult.ConvertFailure<Guid>();
                }

                var workProcessSchemes = new List<CreateWorkProcessSchemeDto>(processResult.Value.WorkTypes.Count);
                foreach (var workType in processResult.Value.WorkTypes)
                {
                    var createWorkflowResult = await _dispatcher.Send(new CreateExternalWorkflowCommand(
                        $"{processResult.Value.Name} - {workType.Name}",
                        "Auto-generated workflow for Azure DevOps work process.",
                        workType), cancellationToken);
                    if (createWorkflowResult.IsFailure)
                        return createWorkflowResult.ConvertFailure<Guid>();

                    workProcessSchemes.Add(CreateWorkProcessSchemeDto.Create(workType.Name, workType.IsActive, createWorkflowResult.Value));
                }

                var createProcessResult = await _dispatcher.Send(new CreateExternalWorkProcessCommand(processResult.Value, workProcessSchemes), cancellationToken);
                if (createProcessResult.IsFailure)
                    return createProcessResult.ConvertFailure<Guid>();

                var updateIntegrationStateResult = await _dispatcher.Send(new UpdateAzureDevOpsWorkProcessIntegrationStateCommand(connectionId, new IntegrationRegistration<Guid, Guid>(workProcessExternalId, createProcessResult.Value)), cancellationToken);

                return updateIntegrationStateResult.IsSuccess
                    ? Result.Success(createProcessResult.Value.InternalId)
                    : updateIntegrationStateResult.ConvertFailure<Guid>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while trying to initialize a work process {WorkProcessExternalId} from Azure DevOps for connection {ConnectionId}.", workProcessExternalId, connectionId);
            return Result.Failure<Guid>($"An error occurred while trying to initialize a work process {workProcessExternalId} from Azure DevOps for connection {connectionId}.");
        }
    }

    public async Task<Result<Guid>> InitWorkspaceIntegration(Guid connectionId, Guid workspaceExternalId, string workspaceKey, string workspaceName, string? externalViewWorkItemUrlTemplate, CancellationToken cancellationToken)
    {
        try
        {
            using (_logger.BeginScope(new Dictionary<string, object> { ["ConnectionId"] = connectionId, ["WorkspaceExternalId"] = workspaceExternalId }))
            {
                var connectionCheckResult = await GetValidConnectionDetailsForWorkspace(connectionId, workspaceExternalId, cancellationToken);
                if (connectionCheckResult.IsFailure)
                    return connectionCheckResult.ConvertFailure<Guid>();

                var exists = await _dispatcher.Send(new ExternalWorkspaceExistsQuery(workspaceExternalId.ToString()), cancellationToken);
                if (exists)
                {
                    _logger.LogError("Unable to initialize a workspace {WorkspaceExternalId} from Azure DevOps for connection {ConnectionId} because it is already integrated.", workspaceExternalId, connectionId);
                    return Result.Failure<Guid>($"Unable to initialize a workspace {workspaceExternalId} from Azure DevOps for connection {connectionId} because it is already integrated.");
                }

                var isDuplicateKey = await _dispatcher.Send(new WorkspaceKeyExistsQuery(workspaceKey), cancellationToken);
                if (isDuplicateKey)
                {
                    _logger.LogWarning("Unable to initialize a workspace {WorkspaceExternalId} from Azure DevOps for connection {ConnectionId} because the workspace key {WorkspaceKey} is already in use.", workspaceExternalId, connectionId, workspaceKey);
                    return Result.Failure<Guid>($"Unable to initialize a workspace {workspaceExternalId} from Azure DevOps for connection {connectionId} because the workspace key {workspaceKey} is already in use.");
                }

                var importWorkspacesResult = await SyncOrganizationConfiguration(connectionId, cancellationToken);
                if (importWorkspacesResult.IsFailure)
                    return importWorkspacesResult.ConvertFailure<Guid>();

                var connectionResult = await GetValidConnectionDetailsForWorkspace(connectionId, workspaceExternalId, cancellationToken);
                if (connectionResult.IsFailure)
                    return connectionResult.ConvertFailure<Guid>();

                var systemId = connectionResult.Value.SystemId;
                if (string.IsNullOrWhiteSpace(systemId))
                {
                    _logger.LogError("The connection {ConnectionId} is missing systemId.", connectionId);
                    return Result.Failure<Guid>("The connection is missing systemId. Please re-sync your connection and try again.");
                }

                var azdoConnection = new AzureDevOpsConnectionContext(connectionResult.Value.Configuration.OrganizationUrl, connectionResult.Value.Configuration.PersonalAccessToken);

                var workspaceResult = await ExternalCallMeasure.MeasureAsync(_logger, "InitWorkspaceIntegration_GetWorkspace", () => _azureDevOpsService.GetWorkspace(azdoConnection, workspaceExternalId, cancellationToken));
                if (workspaceResult.IsFailure)
                    return workspaceResult.ConvertFailure<Guid>();

                var createWorkspaceResult = await _dispatcher.Send(new CreateExternalWorkspaceCommand(Connector.AzureDevOps, systemId, workspaceResult.Value, new WorkspaceKey(workspaceKey), workspaceName, externalViewWorkItemUrlTemplate), cancellationToken);
                if (createWorkspaceResult.IsFailure)
                    return createWorkspaceResult.ConvertFailure<Guid>();

                var updateIntegrationStateResult = await _dispatcher.Send(new UpdateAzureDevOpsWorkspaceIntegrationStateCommand(connectionId, new IntegrationRegistration<Guid, Guid>(workspaceExternalId, createWorkspaceResult.Value)), cancellationToken);

                return updateIntegrationStateResult.IsSuccess
                    ? Result.Success(createWorkspaceResult.Value.InternalId)
                    : updateIntegrationStateResult.ConvertFailure<Guid>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while trying to initialize a workspace {WorkspaceExternalId} from Azure DevOps for connection {ConnectionId}.", workspaceExternalId, connectionId);
            return Result.Failure<Guid>($"An error occurred while trying to initialize a workspace {workspaceExternalId} from Azure DevOps for connection {connectionId}.");
        }
    }

    private async Task<Result<AzureDevOpsConnectionDetailsDto>> GetValidConnectionDetailsForWorkProcess(Guid connectionId, Guid workProcessExternalId, CancellationToken cancellationToken)
    {
        var result = await GetValidConnectionDetails(connectionId, cancellationToken);
        if (result.IsFailure)
            return result;

        if (result.Value.Configuration.WorkProcesses.All(w => w.ExternalId != workProcessExternalId))
        {
            _logger.LogError("The work process {WorkProcessExternalId} is not linked to connection {ConnectionId}.", workProcessExternalId, connectionId);
            return Result.Failure<AzureDevOpsConnectionDetailsDto>($"The work process {workProcessExternalId} is not linked to connection {connectionId}.");
        }

        return Result.Success(result.Value);
    }

    private async Task<Result<AzureDevOpsConnectionDetailsDto>> GetValidConnectionDetailsForWorkspace(Guid connectionId, Guid workspaceExternalId, CancellationToken cancellationToken)
    {
        var result = await GetValidConnectionDetails(connectionId, cancellationToken);
        if (result.IsFailure)
            return result;

        if (result.Value.Configuration.Workspaces.All(w => w.ExternalId != workspaceExternalId))
        {
            _logger.LogError("The workspace {WorkspaceExternalId} is not linked to connection {ConnectionId}.", workspaceExternalId, connectionId);
            return Result.Failure<AzureDevOpsConnectionDetailsDto>($"The workspace {workspaceExternalId} is not linked to connection {connectionId}.");
        }

        return Result.Success(result.Value);
    }

    private async Task<Result<AzureDevOpsConnectionDetailsDto>> GetValidConnectionDetails(Guid connectionId, CancellationToken cancellationToken)
    {
        var connection = await _dispatcher.Send(new GetAzureDevOpsConnectionQuery(connectionId), cancellationToken);
        if (connection is null)
        {
            _logger.LogError("Unable to find Azure DevOps connection with id {ConnectionId}.", connectionId);
            return Result.Failure<AzureDevOpsConnectionDetailsDto>($"Unable to find Azure DevOps connection with id {connectionId}.");
        }
        else if (connection.IsValidConfiguration is false || connection.IsActive is false)
        {
            _logger.LogError("The configuration for Azure DevOps connection {ConnectionId} is not valid.", connectionId);
            return Result.Failure<AzureDevOpsConnectionDetailsDto>($"The configuration for connection {connectionId} is not valid.");
        }

        return Result.Success(connection);
    }
}
