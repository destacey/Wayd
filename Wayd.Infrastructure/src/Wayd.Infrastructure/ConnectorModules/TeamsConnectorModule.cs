using Microsoft.Extensions.DependencyInjection;
using Wayd.AppIntegration.Application.Connections.Managers;
using Wayd.Common.Application.Interfaces.Communication;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Integrations.Abstractions;
using Wayd.Integrations.MicrosoftGraph;

namespace Wayd.Infrastructure.ConnectorModules;

public sealed class TeamsConnectorModule : IConnectorModule
{
    public Connector Connector => Connector.Teams;

    public IReadOnlyList<ConnectorCapability> Capabilities { get; } = [ConnectorCapability.Communication];

    public void Register(IServiceCollection services)
    {
        services.AddScoped<ITeamsCommunicationGraphClient, TeamsCommunicationGraphClient>();
        services.AddKeyedTransient<IConnectionCommunicationTest, TeamsConnectionCommunicationTest>(Connector.Teams);
    }
}
