using System.Text.Json;
using Ardalis.GuardClauses;
using Wayd.Integrations.AzureDevOps.Extensions;
using Wayd.Integrations.AzureDevOps.Models.Converters;
using RestSharp;
using RestSharp.Serializers.Json;

namespace Wayd.Integrations.AzureDevOps.Clients;

internal abstract class BaseClient
{
    protected readonly RestClient _client;
    protected readonly string _token;
    protected readonly string _apiVersion;

    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new ReportingWorkItemLinkResponseConverter() }
    };

    // The HttpClient must come from IHttpClientFactory (the AzureDevOpsHttpClient.Name named client):
    // the factory owns handler pooling/lifetime and applies the host's resilience pipeline
    // (retry honoring Retry-After, timeouts, circuit breaker). RestClient here is a thin per-request
    // wrapper over that shared handler chain and intentionally does not own or dispose the HttpClient.
    internal BaseClient(HttpClient httpClient, string organizationUrl, string token, string apiVersion)
    {
        Guard.Against.Null(httpClient, nameof(httpClient));
        Guard.Against.NullOrWhiteSpace(organizationUrl, nameof(organizationUrl));
        Guard.Against.NullOrWhiteSpace(token, nameof(token));
        Guard.Against.NullOrWhiteSpace(apiVersion, nameof(apiVersion));

        _token = token;
        _apiVersion = apiVersion;

        var options = new RestClientOptions(organizationUrl);

        _client = new RestClient(httpClient, options, configureSerialization: s => s.UseSystemTextJson(_jsonSerializerOptions));
    }

    protected void SetupRequest(RestRequest request, bool includePreviewTag = false)
    {
        request.AddAcceptHeaderWithApiVersion(_apiVersion, includePreviewTag);
        request.AddAuthorizationHeaderForPersonalAccessToken(_token);
    }

    // RestSharp's ExecuteAsync never throws for a failed request — including a genuine
    // cancellation of the caller's token mid-request, which it catches internally and reports as
    // an unsuccessful response with ErrorException set to the OperationCanceledException, rather
    // than letting it propagate. Left unchecked, that makes a cancelled sync run indistinguishable
    // from an ordinary HTTP failure to every caller up the stack (the AzDO services' own
    // `catch (OperationCanceledException) { throw; }` guards only catch cancellation thrown
    // directly in their own frame — not one already absorbed a layer down, inside RestSharp).
    // Re-throwing it here, once, restores real propagation for every client call site.
    protected async Task<RestResponse<T>> ExecuteAsync<T>(RestRequest request, CancellationToken cancellationToken)
    {
        var response = await _client.ExecuteAsync<T>(request, cancellationToken).ConfigureAwait(false);
        if (response.ErrorException is OperationCanceledException operationCanceledException)
            throw operationCanceledException;

        return response;
    }
}
