using Microsoft.Extensions.DependencyInjection;
using Wayd.AppIntegration.Application.Connections.Managers;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
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

        // The personal access token is sent to this address, so only an absolute https URL is accepted.
        services.AddOptions<AzureDevOpsOptions>()
            .BindConfiguration(AzureDevOpsOptions.SectionName)
            .Validate(o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
                $"{AzureDevOpsOptions.SectionName}:{nameof(AzureDevOpsOptions.BaseUrl)} must be an absolute https URL.")
            .ValidateOnStart();
        services.AddSingleton<IAzureDevOpsUrlResolver, AzureDevOpsUrlResolver>();

        services.AddTransient<IAzureDevOpsService, AzureDevOpsService>();
        // Shared by every connection's source in a run, so a shared process is synced once per run.
        services.AddSingleton<AzureDevOpsWorkProcessSyncTracker>();
        services.AddKeyedTransient<IWorkItemSource, AzureDevOpsWorkItemSource>(Connector.AzureDevOps);
        services.AddScoped<ISyncableConnectionDescriptorBuilder, AzureDevOpsConnectionDescriptorBuilder>();
    }
}
