using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Wayd.Integrations.AzureDevOps.Clients;
using Wayd.Integrations.AzureDevOps.Models;
using Wayd.Integrations.AzureDevOps.Models.WorkItems;

namespace Wayd.Integrations.AzureDevOps.Services;

internal sealed class WorkItemService(HttpClient httpClient, string organizationUrl, string token, string apiVersion, ILogger<WorkItemService> logger)
{
    private readonly WorkItemClient _workItemClient = new(httpClient, organizationUrl, token, apiVersion);
    private readonly ILogger<WorkItemService> _logger = logger;

    public async Task<Result<List<WorkItemResponse>>> GetWorkItems(string projectName, DateTime lastChangedDate, string[] workItemTypes, CancellationToken cancellationToken)
    {
        try
        {
            var workItemIds = await _workItemClient.GetWorkItemIds(projectName, lastChangedDate, workItemTypes, excludeWorkItemTypes: false, cancellationToken).ConfigureAwait(false);

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("{WorkItemIdCount} work item ids found for project {Project}", workItemIds.Length, projectName);

            if (workItemIds.Length == 0)
            {
                return Result.Success<List<WorkItemResponse>>([]);
            }

            // TODO: add cancellation process

            // TODO: make this configurable
            string[] fields =
            [
                "System.CreatedDate",
                "System.CreatedBy",
                "System.ChangedDate",
                "System.ChangedBy",
                "System.State",
                "System.Title",
                "System.WorkItemType",

                "System.Parent",
                "System.AreaId",
                "System.AssignedTo",
                "System.IterationId",
                "Microsoft.VSTS.Common.Priority",
                "Microsoft.VSTS.Common.StackRank",
                // Each process uses its own estimate field (Agile StoryPoints, Scrum Effort, CMMI Size),
                // and an item can hold several. A field the type doesn't carry is simply absent.
                "Microsoft.VSTS.Scheduling.StoryPoints",
                "Microsoft.VSTS.Scheduling.Effort",
                "Microsoft.VSTS.Scheduling.Size",
                "Microsoft.VSTS.Common.ActivatedDate",
                "Microsoft.VSTS.Common.ClosedDate",
                "System.Tags"
            ];

            var workitems = await _workItemClient.GetWorkItems(projectName, workItemIds, fields, cancellationToken).ConfigureAwait(false);

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("{WorkItemCount} work items found for project {Project}", workitems.Count, projectName);

            return Result.Success(workitems);
        }
        catch (OperationCanceledException)
        {
            // A genuine cancellation (caller's token fired) is not a sync failure — let it
            // propagate so the caller's cancellation handling (e.g. marking a sync run
            // cancelled rather than partially failed) actually runs.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception thrown getting work items for project {Project} from Azure DevOps", projectName);
            return Result.Failure<List<WorkItemResponse>>(ex.Message);
        }
    }

    public async Task<Result<List<ReportingWorkItemLinkResponse>>> GetParentLinkChanges(string projectName, DateTime lastChangedDate, string[] workItemTypes, CancellationToken cancellationToken)
    {
        try
        {
            string[] linkTypes = ["System.LinkTypes.Hierarchy"];

            var links = await _workItemClient.GetWorkItemLinkChanges(projectName, lastChangedDate, linkTypes, workItemTypes, cancellationToken).ConfigureAwait(false);

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("{LinkCount} parent link changes found for project {Project}", links.Count, projectName);

            return Result.Success(links);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception thrown getting parent link changes for project {Project} from Azure DevOps", projectName);
            return Result.Failure<List<ReportingWorkItemLinkResponse>>(ex.Message);
        }
    }

    public async Task<Result<List<ReportingWorkItemLinkResponse>>> GetDependencyLinkChanges(string projectName, DateTime lastChangedDate, string[] workItemTypes, CancellationToken cancellationToken)
    {
        try
        {
            string[] linkTypes = ["System.LinkTypes.Dependency"];

            var links = await _workItemClient.GetWorkItemLinkChanges(projectName, lastChangedDate, linkTypes, workItemTypes, cancellationToken).ConfigureAwait(false);

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("{LinkCount} dependency link changes found for project {Project}", links.Count, projectName);

            return Result.Success(links);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception thrown getting dependency link changes for project {Project} from Azure DevOps", projectName);
            return Result.Failure<List<ReportingWorkItemLinkResponse>>(ex.Message);
        }
    }

    /// <summary>The fields Wayd keeps history for, plus those that place a revision.</summary>
    private static readonly string[] _revisionFields =
    [
        "System.Id",
        "System.Rev",
        "System.ChangedDate",
        "System.WorkItemType",
        "System.State",
        "System.IterationId",
        "System.AssignedTo",
        "Microsoft.VSTS.Scheduling.StoryPoints",
        "Microsoft.VSTS.Scheduling.Effort",
        "Microsoft.VSTS.Scheduling.Size",
    ];

    public async Task<Result<BatchResponse<ReportingWorkItemRevisionResponse>>> GetWorkItemRevisions(string projectName, string? continuationToken, string[] workItemTypes, CancellationToken cancellationToken)
    {
        try
        {
            var batch = await _workItemClient.GetWorkItemRevisions(projectName, continuationToken, _revisionFields, workItemTypes, cancellationToken).ConfigureAwait(false);

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("{RevisionCount} work item revisions found for project {Project}", batch.Values.Count, projectName);

            return Result.Success(batch);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception thrown getting work item revisions for project {Project} from Azure DevOps", projectName);
            return Result.Failure<BatchResponse<ReportingWorkItemRevisionResponse>>(ex.Message);
        }
    }

    /// <summary>
    /// Reads each item's revisions. An item that fails, after the HTTP client's own retries, is
    /// logged and left out rather than failing the rest: the caller records every item as tried, so
    /// one unreadable item cannot block the others on every sync.
    /// </summary>
    public async Task<Result<List<ReportingWorkItemRevisionResponse>>> GetRevisionsOfWorkItems(IReadOnlyCollection<int> workItemIds, CancellationToken cancellationToken)
    {
        var revisions = new List<ReportingWorkItemRevisionResponse>();
        foreach (var workItemId in workItemIds)
        {
            try
            {
                revisions.AddRange(await _workItemClient.GetRevisionsOfWorkItem(workItemId, cancellationToken).ConfigureAwait(false));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping the revisions of work item {WorkItemId}: Azure DevOps did not return them", workItemId);
            }
        }

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("{RevisionCount} revisions found for {WorkItemCount} work items", revisions.Count, workItemIds.Count);

        return Result.Success(revisions);
    }

    public async Task<Result<int[]>> GetDeletedWorkItemIds(string projectName, DateTime lastChangedDate, string[] syncedWorkItemTypes, CancellationToken cancellationToken)
    {
        try
        {
            int[] recycleBinIds = await _workItemClient.GetDeletedWorkItemIds(projectName, cancellationToken).ConfigureAwait(false);

            // A work item that changed to a type outside the synced set looks "deleted" from the
            // workspace's perspective. Querying NOT IN over the synced types catches every other
            // type — including custom types in inherited processes — without maintaining a list of
            // non-synced type names. With no type filter every type is synced, so only the recycle
            // bin applies.
            int[] typeChangedIds = syncedWorkItemTypes.Length > 0
                ? await _workItemClient.GetWorkItemIds(projectName, lastChangedDate, syncedWorkItemTypes, excludeWorkItemTypes: true, cancellationToken).ConfigureAwait(false)
                : [];

            int[] deletedWorkItemIds = [.. recycleBinIds.Concat(typeChangedIds).Distinct()];

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("{WorkItemIdCount} deleted work item ids found for project {Project}", deletedWorkItemIds.Length, projectName);

            return Result.Success(deletedWorkItemIds);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception thrown getting deleted work item ids for project {Project} from Azure DevOps", projectName);
            return Result.Failure<int[]>(ex.Message);
        }
    }
}
