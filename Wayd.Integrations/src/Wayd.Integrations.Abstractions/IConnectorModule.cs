using Microsoft.Extensions.DependencyInjection;

namespace Wayd.Integrations.Abstractions;

/// <summary>
/// Self-contained registration manifest for one connector: the connector's identity, the
/// capabilities it supports, and the DI wiring for its sources, descriptor builders, init probes,
/// and HTTP clients. One implementation per connector, discovered by assembly scan at startup —
/// adding a connector means adding a module, not editing shared registration code.
///
/// Modules declare behavior wiring only. EF entity configuration, DbSets, and migrations stay
/// central by design.
/// </summary>
public interface IConnectorModule
{
    Connector Connector { get; }

    /// <summary>
    /// The capabilities this connector supports. Must agree with
    /// <c>ConnectorExtensions.GetCapabilities</c> and with the ports the module registers (a
    /// sync capability requires its keyed source) — architecture tests enforce both.
    /// </summary>
    IReadOnlyList<ConnectorCapability> Capabilities { get; }

    void Register(IServiceCollection services);
}
