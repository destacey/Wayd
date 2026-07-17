using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Domain.Models;

namespace Wayd.AppIntegration.Application.Connections.Commands.AzureDevOps;

public sealed record SyncAzureDevOpsConnectionConfigurationCommand(
    Guid ConnectionId,
    IEnumerable<AzureDevOpsBoardsWorkProcess> WorkProcesses,
    IEnumerable<AzureDevOpsBoardsWorkspace> Workspaces,
    IEnumerable<IntegrationRegistration<Guid, Guid>> WorkProcessIntegrationRegistrations,
    List<IExternalTeam> Teams)
    : ICommand;

internal sealed class SyncAzureDevOpsConnectionConfigurationCommandHandler(IAppIntegrationDbContext appIntegrationDbContext, IDateTimeProvider dateTimeProvider, ILogger<SyncAzureDevOpsConnectionConfigurationCommandHandler> logger, IAzureDevOpsService azureDevOpsService, IDispatcher dispatcher) : ICommandHandler<SyncAzureDevOpsConnectionConfigurationCommand>
{
    private const string AppRequestName = nameof(SyncAzureDevOpsConnectionConfigurationCommand);

    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly Instant _timestamp = dateTimeProvider.Now;
    private readonly ILogger<SyncAzureDevOpsConnectionConfigurationCommandHandler> _logger = logger;
    private readonly IAzureDevOpsService _azureDevOpsService = azureDevOpsService;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result> Handle(SyncAzureDevOpsConnectionConfigurationCommand request, CancellationToken cancellationToken)
    {
        var connection = await _appIntegrationDbContext.AzureDevOpsBoardsConnections.FirstOrDefaultAsync(c => c.Id == request.ConnectionId, cancellationToken);
        if (connection is null)
        {
            _logger.LogError("Unable to find Azure DevOps connection with id {ConnectionId}.", request.ConnectionId);
            return Result.Failure($"Unable to find Azure DevOps connection with id {request.ConnectionId}.");
        }


        // this is a temporary process to set the SystemId if it is missing
        if (string.IsNullOrWhiteSpace(connection.SystemId))
        {
            var setSystemIdResult = await SetConnectionSystemId(connection, cancellationToken);
            if (setSystemIdResult.IsFailure)
                return setSystemIdResult;

            await _appIntegrationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Set SystemId for Azure DevOps connection {ConnectionId} to {SystemId}.", connection.Id, connection.SystemId);

            // get workspace internal ids after setting system id
            var workspaceIds = connection.Configuration.Workspaces.Where(w => w.IntegrationState?.InternalId != null).Select(w => w.IntegrationState!.InternalId).ToList();

            var setSystemIdOnWorkspacesResult = await _dispatcher.Send(new SetSystemIdOnExternalWorkspacesCommand(workspaceIds, connection.Connector, connection.SystemId!), cancellationToken);
            if (setSystemIdOnWorkspacesResult.IsFailure)
            {
                _logger.LogError("Failed to set SystemId on external workspaces for connection {ConnectionId}. {Error}", connection.Id, setSystemIdOnWorkspacesResult.Error);
                return Result.Failure($"Failed to set SystemId on external workspaces for connection {connection.Id}. {setSystemIdOnWorkspacesResult.Error}");
            }
        }

        var importWorkProcessesResult = connection.SyncProcesses(request.WorkProcesses, _timestamp);
        if (importWorkProcessesResult.IsFailure)
        {
            return LogAndReturnFailure(importWorkProcessesResult);
        }

        var liveInternalIds = request.WorkProcessIntegrationRegistrations
            .Select(r => r.IntegrationState.InternalId)
            .ToHashSet();

        foreach (var workProcess in connection.Configuration.WorkProcesses)
        {
            var workProcessIntegrationRegistration = request.WorkProcessIntegrationRegistrations.FirstOrDefault(w => w.ExternalId == workProcess.ExternalId);
            if (workProcessIntegrationRegistration is not null)
            {
                var result = connection.UpdateWorkProcessIntegrationState(workProcessIntegrationRegistration, _timestamp);
                if (result.IsFailure)
                {
                    return LogAndReturnFailure(result);
                }
                continue;
            }

            // No registration found for this work process. If we still hold an IntegrationState
            // pointing at a Wayd.Work.WorkProcess that no longer exists, clear the dangling
            // pointer so the UI stops 404ing and the user can re-initialize.
            if (workProcess.IntegrationState is null) continue;

            if (liveInternalIds.Contains(workProcess.IntegrationState.InternalId)) continue;

            _logger.LogWarning(
                "Clearing dangling IntegrationState for Azure DevOps work process {WorkProcessExternalId} on connection {ConnectionId}. The referenced Wayd.Work.WorkProcess {WorkProcessInternalId} no longer exists.",
                workProcess.ExternalId, connection.Id, workProcess.IntegrationState.InternalId);

            var clearResult = connection.ClearWorkProcessIntegrationState(workProcess.ExternalId, _timestamp);
            if (clearResult.IsFailure)
            {
                return LogAndReturnFailure(clearResult);
            }
        }

        // Capture workspace WorkProcessIds before sync to detect changes
        var workspaceProcessesBefore = connection.Configuration.Workspaces
            .Where(w => w.IntegrationState is not null)
            .ToDictionary(w => w.ExternalId, w => w.WorkProcessId);

        var importWorkspacesResult = connection.SyncWorkspaces(request.Workspaces, _timestamp);
        if (importWorkspacesResult.IsFailure)
        {
            return LogAndReturnFailure(importWorkspacesResult);
        }

        var syncTeamsResult = connection.SyncTeams(request.Teams, _timestamp);
        if (syncTeamsResult.IsFailure)
        {
            return LogAndReturnFailure(syncTeamsResult);
        }

        await _appIntegrationDbContext.SaveChangesAsync(cancellationToken);

        // Detect and handle workspace process changes
        await UpdateWorkspaceProcessAssignments(connection, workspaceProcessesBefore, cancellationToken);

        return Result.Success();

        Result LogAndReturnFailure(Result result)
        {
            _logger.LogError("Errors occurred while processing {AppRequestName}. {Error}", AppRequestName, result.Error);

            return Result.Failure($"Errors occurred while processing {AppRequestName}.");
        }
    }

    /// <summary>
    /// Detects workspaces whose Azure DevOps process changed and updates the corresponding Wayd Workspace's WorkProcessId.
    /// </summary>
    private async Task UpdateWorkspaceProcessAssignments(AzureDevOpsBoardsConnection connection, Dictionary<Guid, Guid?> workspaceProcessesBefore, CancellationToken cancellationToken)
    {
        foreach (var workspace in connection.Configuration.Workspaces)
        {
            if (workspace.IntegrationState is null || workspace.WorkProcessId is null)
                continue;

            // Check if this workspace's process changed during sync
            if (!workspaceProcessesBefore.TryGetValue(workspace.ExternalId, out var previousProcessId)
                || previousProcessId == workspace.WorkProcessId)
                continue;

            var workspaceInternalId = workspace.IntegrationState.InternalId;

            _logger.LogInformation(
                "Detected work process change for workspace {WorkspaceId}: {OldProcessId} -> {NewProcessId}. Updating Wayd workspace.",
                workspaceInternalId, previousProcessId, workspace.WorkProcessId);
    
            var changeResult = await _dispatcher.Send(
                new ChangeExternalWorkspaceWorkProcessCommand(workspaceInternalId, workspace.WorkProcessId.Value),
                cancellationToken);

            if (changeResult.IsFailure)
            {
                // Log but don't fail the overall sync — the workspace will be updated on the next sync
                // once the new process is initialized
                _logger.LogWarning(
                    "Failed to update work process for workspace {WorkspaceId}. The new process may not be initialized yet. Error: {Error}",
                    workspaceInternalId, changeResult.Error);
            }
        }
    }

    private async Task<Result> SetConnectionSystemId(AzureDevOpsBoardsConnection connection, CancellationToken cancellationToken)
    {
        var config = new AzureDevOpsBoardsConnectionConfiguration(connection.Configuration.Organization, connection.Configuration.PersonalAccessToken);

        var systemIdResult = await _azureDevOpsService.GetSystemId(config.OrganizationUrl, config.PersonalAccessToken, cancellationToken);
        if (systemIdResult.IsFailure)
        {
            _logger.LogError("Unable to get system id for Azure DevOps connection {ConnectionId}. {Error}", connection.Id, systemIdResult.Error);
            return Result.Failure($"Unable to get system id for Azure DevOps connection for organization {connection.Configuration.Organization}.");
        }

        var setSystemIdResult = connection.SetSystemId(systemIdResult.Value);
        if (setSystemIdResult.IsFailure)
        {
            _logger.LogError("Error setting system id for connection {ConnectionId} to {SystemId}. {Error}", connection.Id, systemIdResult.Value, setSystemIdResult.Error);
            return Result.Failure($"Error setting system id for connection {connection.Id} to {systemIdResult.Value}. {setSystemIdResult.Error}");
        }

        return Result.Success();
    }
}