using Microsoft.Extensions.DependencyInjection;
using Wayd.AppIntegration.Application.Connections.Managers;
using Wayd.Common.Application.Interfaces.ExternalPeople;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Integrations.Abstractions;
using Wayd.Integrations.MicrosoftGraph;

namespace Wayd.Infrastructure.ConnectorModules;

public sealed class EntraConnectorModule : IConnectorModule
{
    public Connector Connector => Connector.Entra;

    public IReadOnlyList<ConnectorCapability> Capabilities { get; } = [ConnectorCapability.People];

    public void Register(IServiceCollection services)
    {
        services.AddScoped<IEntraEmployeeSource, MicrosoftGraphService>();
        services.AddKeyedTransient<IEmployeeSource, EntraEmployeeSource>(Connector.Entra);
        services.AddScoped<ISyncableConnectionDescriptorBuilder, EntraConnectionDescriptorBuilder>();
    }
}
