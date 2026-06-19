using Wayd.Common.Application.Interfaces.ExternalPeople;

namespace Wayd.Integrations.Abstractions;

/// <summary>
/// Optional per-connector init probe: a small call against the upstream system that confirms the
/// configuration is usable and persists structured validation details on the connection.
/// Registered keyed by <see cref="Connector"/> — a connector "supports an init probe" exactly
/// when one is registered for it; consumers resolve by key and treat null as "not supported".
/// </summary>
public interface IConnectionInitProbe
{
    Connector Connector { get; }

    Task<Result<ConnectionInitResult>> Run(Guid connectionId, CancellationToken cancellationToken);
}
