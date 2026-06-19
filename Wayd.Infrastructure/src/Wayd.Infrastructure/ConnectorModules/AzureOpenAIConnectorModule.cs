using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Integrations.Abstractions;

namespace Wayd.Infrastructure.ConnectorModules;

public sealed class AzureOpenAIConnectorModule : IConnectorModule
{
    public Connector Connector => Connector.AzureOpenAI;

    public IReadOnlyList<ConnectorCapability> Capabilities { get; } = [ConnectorCapability.AiProvider];

    public void Register(IServiceCollection services)
    {
        // No sync surface and no shared client — the AI client is constructed per call from the
        // connection's configuration by the handlers that consume it. The module exists as the
        // connector's manifest so the enum↔module and capability architecture tests cover it.
    }
}
