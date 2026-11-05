namespace Wayd.AppIntegration.Application.Interfaces;

public interface IAzureDevOpsInitManager : ISaga
{
    /// <summary>
    /// Refreshes the connection's list of Azure DevOps processes, projects and teams.
    /// </summary>
    Task<Result> SyncOrganizationConfiguration(Guid connectionId, CancellationToken cancellationToken, Guid? syncId = null);

    /// <summary>
    /// Makes sure an Azure DevOps process is set up in Wayd for the connection: links the Wayd work
    /// process when one exists for it, or creates it with its work types, statuses and workflows.
    /// Fails when the Wayd work process exists but is deactivated. Does not refresh the
    /// organization configuration first.
    /// </summary>
    /// <returns>The Wayd work process id.</returns>
    Task<Result<Guid>> EnsureWorkProcessIntegration(Guid connectionId, Guid workProcessExternalId, CancellationToken cancellationToken);

    /// <summary>
    /// Sets up an Azure DevOps project for syncing: creates its Wayd workspace, setting up the
    /// project's work process first when needed.
    /// </summary>
    /// <returns>The Wayd workspace id.</returns>
    Task<Result<Guid>> InitWorkspaceIntegration(Guid connectionId, Guid workspaceExternalId, string workspaceKey, string workspaceName, string? externalViewWorkItemUrlTemplate, CancellationToken cancellationToken);
}
