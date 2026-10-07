using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Models;

namespace Wayd.Common.Application.Interfaces;

public interface IAzureDevOpsService
{
    Task<Result<string>> GetSystemId(AzureDevOpsConnectionContext connection, CancellationToken cancellationToken);
    Task<Result<List<IExternalWorkProcess>>> GetWorkProcesses(AzureDevOpsConnectionContext connection, CancellationToken cancellationToken);
    Task<Result<IExternalWorkProcessConfiguration>> GetWorkProcess(AzureDevOpsConnectionContext connection, Guid processId, CancellationToken cancellationToken);
    Task<Result<IExternalWorkspaceConfiguration>> GetWorkspace(AzureDevOpsConnectionContext connection, Guid workspaceId, CancellationToken cancellationToken);
    Task<Result<List<IExternalWorkspace>>> GetWorkspaces(AzureDevOpsConnectionContext connection, CancellationToken cancellationToken);
    Task<Result<List<IExternalTeam>>> GetTeams(AzureDevOpsConnectionContext connection, Guid[] projectIds, CancellationToken cancellationToken);
    Task<Result<List<IExternalIteration<AzdoIterationMetadata>>>> GetIterations(AzureDevOpsConnectionContext connection, string projectName, Dictionary<Guid, Guid?> teamSettings, CancellationToken cancellationToken);
    Task<Result<List<IExternalWorkItem>>> GetWorkItems(AzureDevOpsConnectionContext connection, string projectName, DateTime lastChangedDate, string[] workItemTypes, Dictionary<Guid, Guid?> teamSettings, CancellationToken cancellationToken);
    Task<Result<List<IExternalWorkItemLink>>> GetParentLinkChanges(AzureDevOpsConnectionContext connection, string projectName, DateTime lastChangedDate, string[] workItemTypes, CancellationToken cancellationToken);
    Task<Result<List<IExternalWorkItemLink>>> GetDependencyLinkChanges(AzureDevOpsConnectionContext connection, string projectName, DateTime lastChangedDate, string[] workItemTypes, CancellationToken cancellationToken);
    Task<Result<int[]>> GetDeletedWorkItemIds(AzureDevOpsConnectionContext connection, string projectName, DateTime lastChangedDate, string[] workItemTypes, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one page of the project's work item revisions of the given types (every type when
    /// none are given), continuing from <paramref name="continuationToken"/>, or from the project's
    /// first revision when it is null.
    /// </summary>
    Task<Result<AzureDevOpsWorkItemRevisionsBatch>> GetWorkItemRevisions(AzureDevOpsConnectionContext connection, string projectName, string? continuationToken, string[] workItemTypes, CancellationToken cancellationToken);

    /// <summary>
    /// Reads every revision of each given work item, whichever projects it was in and whatever its
    /// type was. Items that no longer exist are left out.
    /// </summary>
    Task<Result<List<IExternalWorkItemRevision>>> GetRevisionsOfWorkItems(AzureDevOpsConnectionContext connection, IReadOnlyCollection<int> workItemIds, CancellationToken cancellationToken);
    Task<Result> TestConnection(AzureDevOpsConnectionContext connection);
}
