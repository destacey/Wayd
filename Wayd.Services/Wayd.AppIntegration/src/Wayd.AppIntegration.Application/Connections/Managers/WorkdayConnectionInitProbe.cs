using Wayd.AppIntegration.Application.Connections.Commands.Workday;
using Wayd.Common.Application.Interfaces.ExternalPeople;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Integrations.Abstractions;

namespace Wayd.AppIntegration.Application.Connections.Managers;

/// <summary>
/// Keyed init-probe port for Workday — wraps the existing <see cref="InitWorkdayConnectionCommand"/>
/// so the connections controller can dispatch probes by connector key instead of switching on
/// connection types.
/// </summary>
public sealed class WorkdayConnectionInitProbe(IDispatcher dispatcher) : IConnectionInitProbe
{
    private readonly IDispatcher _dispatcher = dispatcher;

    public Connector Connector => Connector.Workday;

    public Task<Result<ConnectionInitResult>> Run(Guid connectionId, CancellationToken cancellationToken) =>
        _dispatcher.Send(new InitWorkdayConnectionCommand(connectionId), cancellationToken);
}
