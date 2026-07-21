using Wayd.Integrations.AzureDevOps.Models;
using RestSharp;

namespace Wayd.Integrations.AzureDevOps.Clients;

internal sealed class GeneralClient : BaseClient
{
    internal GeneralClient(HttpClient httpClient, string organizationUrl, string token, string apiVersion)
        : base(httpClient, organizationUrl, token, apiVersion)
    { }

    internal async Task<RestResponse<ConnectionDataResponse>> GetConnectionData(CancellationToken cancellationToken)
    {
        var request = new RestRequest("/_apis/connectionData", Method.Get);
        SetupRequest(request, true);  // still in preview only

        return await ExecuteAsync<ConnectionDataResponse>(request, cancellationToken)
            .ConfigureAwait(false);
    }
}
