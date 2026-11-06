namespace Wayd.Integrations.Abstractions;

/// <summary>
/// Optional per-connector communication test: proves the connection can reach Microsoft Graph
/// for chat and calendar writes. Registered keyed by <see cref="Connector"/> — a connector
/// supports a communication test exactly when one is registered for it.
/// </summary>
public interface IConnectionCommunicationTest
{
    Connector Connector { get; }

    /// <param name="userAssertion">
    /// Unused by Microsoft Teams. The Teams test uses the connection's client secret.
    /// </param>
    Task<Result> Run(
        Guid connectionId,
        string? userAssertion,
        CancellationToken cancellationToken);
}
