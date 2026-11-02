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

    public Task<Result> Run(
        Guid connectionId,
        string? userAssertion,
        CancellationToken cancellationToken)
    {
        // The Teams app registration lives in its own tenant. A Wayd sign-in token cannot be
        // exchanged on-behalf-of for that app, so the test uses the connection's client secret only.
        _ = userAssertion;
        return _dispatcher.Send(new TestTeamsConnectionCommand(connectionId), cancellationToken);
    }
}
