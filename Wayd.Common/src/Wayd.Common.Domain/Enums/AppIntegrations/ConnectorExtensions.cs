namespace Wayd.Common.Domain.Enums.AppIntegrations;

public static class ConnectorExtensions
{
    private static readonly ConnectorCapability[] _workItems = [ConnectorCapability.WorkItems];
    private static readonly ConnectorCapability[] _people = [ConnectorCapability.People];
    private static readonly ConnectorCapability[] _aiProvider = [ConnectorCapability.AiProvider];

    /// <summary>
    /// Returns all capabilities a connector supports. Multi-surface connectors, such as GitHub,
    /// can support more than one capability through one connection. This switch is the single
    /// declaration per connector — it is interim until every capability has a source port, at
    /// which point support derives from the ports each connector registers.
    /// </summary>
    public static IReadOnlyList<ConnectorCapability> GetCapabilities(this Connector connector) => connector switch
    {
        Connector.AzureDevOps => _workItems,
        Connector.AzureOpenAI => _aiProvider,
        Connector.Entra => _people,
        Connector.Workday => _people,
        _ => throw new InvalidOperationException($"No capabilities are declared for connector '{connector}'.")
    };

    public static bool HasCapability(this Connector connector, ConnectorCapability capability) =>
        connector.GetCapabilities().Contains(capability);
}
