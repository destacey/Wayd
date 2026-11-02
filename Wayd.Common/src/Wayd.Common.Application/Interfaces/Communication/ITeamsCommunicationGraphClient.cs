namespace Wayd.Common.Application.Interfaces.Communication;

/// <summary>
/// Outbound Microsoft Graph calls for Teams communication connections. Separate from
/// <see cref="Interfaces.ExternalPeople.IEntraEmployeeSource"/> — people sync stays read-only.
/// </summary>
public interface ITeamsCommunicationGraphClient
{
    /// <summary>
    /// Proves the connection's application permissions: looks up the recipient, creates a Teams
    /// chat, and creates a calendar event. Does not post a chat message.
    /// </summary>
    Task<Result> RunHelloWorldTestAsync(
        TeamsGraphCredentials credentials,
        string recipientEmail,
        CancellationToken cancellationToken);
}
