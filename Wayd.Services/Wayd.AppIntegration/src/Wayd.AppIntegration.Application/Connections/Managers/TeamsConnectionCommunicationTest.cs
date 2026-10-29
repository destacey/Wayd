using Wayd.AppIntegration.Application.Connections.Commands.Teams;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Integrations.Abstractions;

namespace Wayd.AppIntegration.Application.Connections.Managers;

/// <summary>
/// Keyed communication test for Teams — wraps <see cref="TestTeamsConnectionCommand"/> so the
/// connections controller can dispatch tests by connector key.
/// </summary>
public sealed class TeamsConnectionCommunicationTest(IDispatcher dispatcher) : IConnectionCommunicationTest
{
    private readonly IDispatcher _dispatcher = dispatcher;

    public Connector Connector => Connector.Teams;

    public Task<Result> Run(Guid connectionId, CancellationToken cancellationToken) =>
        _dispatcher.Send(new TestTeamsConnectionCommand(connectionId), cancellationToken);
}
