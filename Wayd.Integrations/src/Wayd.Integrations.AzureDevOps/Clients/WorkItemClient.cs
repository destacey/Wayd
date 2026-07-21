using Ardalis.GuardClauses;
using Wayd.Common.Extensions;
using Wayd.Integrations.AzureDevOps.Extensions;
using Wayd.Integrations.AzureDevOps.Models;
using Wayd.Integrations.AzureDevOps.Models.WorkItems;
using RestSharp;

namespace Wayd.Integrations.AzureDevOps.Clients;

internal sealed class WorkItemClient : BaseClient
{
    internal WorkItemClient(HttpClient httpClient, string organizationUrl, string token, string apiVersion)
        : base(httpClient, organizationUrl, token, apiVersion)
    { }

    internal async Task<int[]> GetWorkItemIds(string projectName, DateTime lastChangedDate, string[] workItemTypes, bool excludeWorkItemTypes, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(projectName, nameof(projectName));

        var request = new RestRequest("/_apis/wit/wiql", Method.Post);
        SetupRequest(request);

        int maxResults = 10_000; // this was erroring out with 20_000, even though the API supports up to 20_000
        request.AddQueryParameter("$top", maxResults);
        request.AddQueryParameter("timePrecision", "true");

        int startingId = 0;
        List<int> workItemIds = [];
        while (true)
        {
            var wiql = CreateWiqlQueryForSync(projectName, lastChangedDate, workItemTypes, startingId, excludeWorkItemTypes);

            request.AddJsonBody(wiql);

            var response = await ExecuteAsync<WiqlResponse>(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessful)
            {
                throw new Exception($"Error getting work item ids for project {projectName} from Azure DevOps: {response.GetErrorText()}");
            }
            else if (response.Data?.WorkItems is null || response.Data.WorkItems.Count == 0)
            {
                break;
            }

            var ids = response.Data.WorkItems.Select(wi => wi.Id).ToList();
            workItemIds.AddRange(ids);

            if (ids.Count < maxResults)
            {
                break;
            }

            startingId = ids.Last();
            var bodyParameter = request.Parameters.OfType<BodyParameter>().Single();
            request.RemoveParameter(bodyParameter);
        }

        return [.. workItemIds.Distinct()];
    }

    internal async Task<List<WorkItemResponse>> GetWorkItems(string projectName, int[] workItemIds, string[] fields, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(projectName, nameof(projectName));

        if (workItemIds.Length == 0)
            return [];

        var request = new RestRequest($"/{projectName}/_apis/wit/workitemsbatch", Method.Post);
        SetupRequest(request);

        int maxResults = 200;

        var workItems = new List<WorkItemResponse>();
        var batches = workItemIds.Distinct().Batch(maxResults);
        foreach (var batch in batches)
        {
            request.AddJsonBody(WorkItemsBatchRequest.Create(batch, fields));
            var response = await ExecuteAsync<ListResponse<WorkItemResponse>>(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessful)
            {
                throw new Exception($"Error getting work items for project {projectName} from Azure DevOps: {response.GetErrorText()}");
            }
            else if (response.Data is null)
            {
                throw new Exception($"Successful response, but no work item data returned for project {projectName} from Azure DevOps");
            }

            // An empty batch is legitimate under errorPolicy=omit: every id in it was deleted
            // between the WIQL query and this fetch.
            workItems.AddRange(response.Data.Value);

            var bodyParameter = request.Parameters.OfType<BodyParameter>().Single();
            request.RemoveParameter(bodyParameter);
        }

        return workItems;
    }

    internal async Task<List<ReportingWorkItemLinkResponse>> GetWorkItemLinkChanges(string projectName, DateTime lastChangedDate, string[] linkTypes, string[] workItemTypes, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(projectName, nameof(projectName));

        var request = new RestRequest($"/{projectName}/_apis/wit/reporting/workitemlinks", Method.Get);
        SetupRequest(request);

        if (linkTypes.Length > 0)
        {
            request.AddQueryParameter("linkTypes", string.Join(",", linkTypes));
        }
        if (workItemTypes.Length > 0)
        {
            request.AddQueryParameter("workItemTypes", string.Join(",", workItemTypes));
        }
        request.AddQueryParameter("startDateTime", lastChangedDate.ToString("o"));

        var workItemLinks = new List<ReportingWorkItemLinkResponse>();
        string? continuationToken = null;

        while (true)
        {
            if (!string.IsNullOrEmpty(continuationToken))
            {
                // Remove the existing continuationToken parameter if it exists
                var existingParameter = request.Parameters.FirstOrDefault(p => p.Name == "continuationToken");
                if (existingParameter != null)
                {
                    request.RemoveParameter(existingParameter);
                }

                // Add the new continuationToken parameter
                request.AddQueryParameter("continuationToken", continuationToken);
            }

            var response = await ExecuteAsync<BatchResponse<ReportingWorkItemLinkResponse>>(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessful)
            {
                throw new Exception($"Error getting work item link changes for project {projectName} from Azure DevOps: {response.GetErrorText()}");
            }
            else if (response.Data?.Values is null)
            {
                break;
            }

            workItemLinks.AddRange(response.Data.Values);

            if (response.Data.IsLastBatch)
            {
                break;
            }

            // A non-final batch must advance the watermark token; re-issuing the same request
            // would loop forever on the same page, so treat a stalled token as a protocol error.
            var nextContinuationToken = response.Data.ContinuationToken;
            if (string.IsNullOrEmpty(nextContinuationToken) || nextContinuationToken == continuationToken)
            {
                throw new Exception($"Error getting work item link changes for project {projectName} from Azure DevOps: continuation token did not advance (token: '{nextContinuationToken}').");
            }

            continuationToken = nextContinuationToken;
        }

        return workItemLinks;
    }

    internal async Task<int[]> GetDeletedWorkItemIds(string projectName, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(projectName, nameof(projectName));

        var request = new RestRequest($"/{projectName}/_apis/wit/recyclebin", Method.Get);
        SetupRequest(request);

        var response = await ExecuteAsync<ListResponse<WiqlWorkItemResponse>>(request, cancellationToken).ConfigureAwait(false);

        return response.IsSuccessful
            ? response.Data?.Value.Select(w => w.Id).ToArray() ?? []
            : throw new Exception($"Error getting deleted work item ids for project {projectName} from Azure DevOps: {response.GetErrorText()}");
    }

    private static WiqlRequest CreateWiqlQueryForSync(string project, DateTime dateTime, string[] workItemTypes, int startingId, bool excludeWorkItemTypes)
    {
        var workItemTypesFilter = "";
        if (workItemTypes.Length > 0)
        {
            var typeOperator = excludeWorkItemTypes ? "NOT IN" : "IN";
            workItemTypesFilter = $"AND [System.WorkItemType] {typeOperator} ({string.Join(",", workItemTypes.Select(t => $"'{EscapeWiqlLiteral(t)}'"))})";
        }
        return new()
        {
            Query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{EscapeWiqlLiteral(project)}' AND [System.Id] > {startingId} AND [System.ChangedDate] > '{dateTime:yyyy-MM-ddTHH:mm:ssZ}' {workItemTypesFilter} ORDER BY [System.Id] Asc"
        };
    }

    // WIQL string literals escape embedded single quotes by doubling them.
    private static string EscapeWiqlLiteral(string value) => value.Replace("'", "''");
}
