namespace Wayd.Integrations.AzureDevOps;

/// <summary>
/// The <see cref="System.Net.Http.IHttpClientFactory"/> named client used for all Azure DevOps REST calls.
/// Registered by the Azure DevOps connector module; the host's default resilience pipeline
/// (retry honoring Retry-After, per-attempt/total timeouts, circuit breaker) applies to it.
/// </summary>
public static class AzureDevOpsHttpClient
{
    public const string Name = "azure-devops";
}
