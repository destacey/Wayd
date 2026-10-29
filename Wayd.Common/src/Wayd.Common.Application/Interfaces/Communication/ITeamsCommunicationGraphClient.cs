namespace Wayd.Common.Application.Interfaces.Communication;

/// <summary>
/// Outbound Microsoft Graph calls for Teams communication connections. Separate from
/// <see cref="Interfaces.ExternalPeople.IEntraEmployeeSource"/> — people sync stays read-only.
/// </summary>
public interface ITeamsCommunicationGraphClient
{
    /// <summary>
    /// Proves application permissions with client credentials (user lookup, chat create, calendar
    /// event) and sends a hello-world chat message with a delegated Graph token from
    /// <paramref name="userAssertion"/>.
    /// </summary>
    Task<Result> RunHelloWorldTestAsync(
        TeamsGraphCredentials credentials,
        string recipientEmail,
        string userAssertion,
        CancellationToken cancellationToken);
}
