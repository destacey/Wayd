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
    /// <summary>
    /// The ids of the organization's work items changed after <paramref name="changedSince"/> that
    /// are in none of <paramref name="projectNames"/>: those moved out of a synced project since then,
    /// among others.
    /// </summary>
    Task<Result<int[]>> GetIdsOfWorkItemsChangedOutsideProjects(AzureDevOpsConnectionContext connection, string[] projectNames, DateTime changedSince, CancellationToken cancellationToken);

    /// <summary>
    /// Those of <paramref name="workItemIds"/> now in none of <paramref name="projectNames"/>, read
    /// item by item. An item that no longer exists is left out.
    /// </summary>
    Task<Result<int[]>> GetIdsOfWorkItemsOutsideProjects(AzureDevOpsConnectionContext connection, int[] workItemIds, string[] projectNames, CancellationToken cancellationToken);

    /// <summary>
    /// The ids of the project's work items in its recycle bin and, when <paramref name="typeChangedSince"/>
    /// is given, of those changed since then to a type outside <paramref name="workItemTypes"/>.
    /// </summary>
    /// <param name="typeChangedSince">
    /// Null skips the type-change query. A full sync passes null: from its 1900 watermark the query
    /// returns every item of an unsynced type the project has ever had, none of which Wayd holds.
    /// </param>
    Task<Result<int[]>> GetDeletedWorkItemIds(AzureDevOpsConnectionContext connection, string projectName, DateTime? typeChangedSince, string[] workItemTypes, CancellationToken cancellationToken);

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
