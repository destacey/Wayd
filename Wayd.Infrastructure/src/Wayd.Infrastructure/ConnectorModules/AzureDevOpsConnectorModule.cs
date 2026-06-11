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
        services.AddTransient<IAzureDevOpsService, AzureDevOpsService>();
        services.AddKeyedTransient<IWorkItemSource, AzureDevOpsWorkItemSource>(Connector.AzureDevOps);
        services.AddScoped<ISyncableConnectionDescriptorBuilder, AzureDevOpsConnectionDescriptorBuilder>();
    }
}
