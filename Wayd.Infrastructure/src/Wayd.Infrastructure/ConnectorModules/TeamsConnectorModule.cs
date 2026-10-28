using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Integrations.Abstractions;

namespace Wayd.Infrastructure.ConnectorModules;

public sealed class TeamsConnectorModule : IConnectorModule
{
    public Connector Connector => Connector.Teams;

    public IReadOnlyList<ConnectorCapability> Capabilities { get; } = [ConnectorCapability.Communication];

    public void Register(IServiceCollection services)
    {
        // No sync surface — Graph calls are constructed per use from the connection configuration.
    }
}
