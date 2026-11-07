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
    /// <returns>The Wayd work process id, and whether the process was read from Azure DevOps to create it.</returns>
    Task<Result<WorkProcessIntegration>> EnsureWorkProcessIntegration(Guid connectionId, Guid workProcessExternalId, CancellationToken cancellationToken);

    /// <summary>
    /// Sets up an Azure DevOps project for syncing: creates its Wayd workspace, setting up the
    /// project's work process first when needed.
    /// </summary>
    /// <returns>The Wayd workspace id.</returns>
    Task<Result<Guid>> InitWorkspaceIntegration(Guid connectionId, Guid workspaceExternalId, string workspaceKey, string workspaceName, string? externalViewWorkItemUrlTemplate, CancellationToken cancellationToken);
}

/// <summary>A work process set up for a connection.</summary>
/// <param name="InternalId">The Wayd work process id.</param>
/// <param name="ReadFromSource">
/// Whether it was created just now from Azure DevOps' current process. <c>false</c> when it already existed,
/// on this connection or another, so nothing was read and the caller still has to sync it.
/// </param>
public sealed record WorkProcessIntegration(Guid InternalId, bool ReadFromSource);
