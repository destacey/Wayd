namespace Wayd.AppIntegration.Application.Connections.Commands.AzureDevOps;

/// <summary>
/// Turns syncing on or off for an Azure DevOps project that has been set up on the connection.
/// </summary>
public sealed record SetAzureDevOpsWorkspaceSyncCommand(Guid ConnectionId, Guid WorkspaceExternalId, bool IsEnabled) : ICommand;

/// <summary>
/// Handles <see cref="SetAzureDevOpsWorkspaceSyncCommand"/>.
/// </summary>
public sealed class SetAzureDevOpsWorkspaceSyncCommandHandler(IAppIntegrationDbContext appIntegrationDbContext, ILogger<SetAzureDevOpsWorkspaceSyncCommandHandler> logger) : ICommandHandler<SetAzureDevOpsWorkspaceSyncCommand>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly ILogger<SetAzureDevOpsWorkspaceSyncCommandHandler> _logger = logger;

    /// <summary>
    /// Sets the project's sync state and saves the connection.
    /// </summary>
    public async Task<Result> Handle(SetAzureDevOpsWorkspaceSyncCommand request, CancellationToken cancellationToken)
    {
        var connection = await _appIntegrationDbContext.AzureDevOpsBoardsConnections.FirstOrDefaultAsync(c => c.Id == request.ConnectionId, cancellationToken);
        if (connection is null)
        {
            _logger.LogWarning("Unable to find Azure DevOps connection with id {ConnectionId}.", request.ConnectionId);
            return Result.Failure($"Unable to find Azure DevOps connection with id {request.ConnectionId}.");
        }

        var result = connection.SetWorkspaceSync(request.WorkspaceExternalId, request.IsEnabled);
        if (result.IsFailure)
        {
            _logger.LogWarning("Unable to set sync for workspace {WorkspaceExternalId} on connection {ConnectionId}. {Error}", request.WorkspaceExternalId, request.ConnectionId, result.Error);
            return result;
        }

        await _appIntegrationDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Sync {SyncState} for workspace {WorkspaceExternalId} on connection {ConnectionId}.", request.IsEnabled ? "enabled" : "disabled", request.WorkspaceExternalId, request.ConnectionId);

        return Result.Success();
    }
}
