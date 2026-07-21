using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Wayd.Integrations.AzureDevOps.Clients;
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
                "Microsoft.VSTS.Scheduling.StoryPoints",
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
