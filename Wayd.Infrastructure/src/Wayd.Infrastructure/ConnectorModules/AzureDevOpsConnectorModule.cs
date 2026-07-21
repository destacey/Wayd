using Microsoft.Extensions.DependencyInjection;
using Wayd.AppIntegration.Application.Connections.Managers;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Integrations.Abstractions;
using Wayd.Integrations.AzureDevOps;

namespace Wayd.Infrastructure.ConnectorModules;

public sealed class AzureDevOpsConnectorModule : IConnectorModule
{
    public Connector Connector => Connector.AzureDevOps;

    public IReadOnlyList<ConnectorCapability> Capabilities { get; } = [ConnectorCapability.WorkItems];

    public void Register(IServiceCollection services)
    {
        // Named client shared by all Azure DevOps REST calls. The host's default resilience
        // pipeline (ConfigureHttpClientDefaults in ConfigureServices) applies: retry honoring
        // Retry-After — which is how Azure DevOps signals throttling — 90s per-attempt timeout,
        // 5min total, circuit breaker. HttpClient.Timeout is disabled so the pipeline's total
        // timeout is the single outer bound; the factory's 100s default would otherwise abort
        // a slow-but-retrying request mid-pipeline.
        services.AddHttpClient(AzureDevOpsHttpClient.Name,
            client => client.Timeout = Timeout.InfiniteTimeSpan);

        services.AddTransient<IAzureDevOpsService, AzureDevOpsService>();
        services.AddKeyedTransient<IWorkItemSource, AzureDevOpsWorkItemSource>(Connector.AzureDevOps);
        services.AddScoped<ISyncableConnectionDescriptorBuilder, AzureDevOpsConnectionDescriptorBuilder>();
    }
}
