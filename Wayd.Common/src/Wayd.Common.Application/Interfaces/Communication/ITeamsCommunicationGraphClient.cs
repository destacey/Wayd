namespace Wayd.Common.Application.Interfaces.Communication;

/// <summary>
/// Outbound Microsoft Graph calls for Teams communication connections. Separate from
/// <see cref="Interfaces.ExternalPeople.IEntraEmployeeSource"/> — people sync stays read-only.
/// </summary>
public interface ITeamsCommunicationGraphClient
{
    /// <summary>
    /// Sends a Teams chat message to <paramref name="recipientEmail"/> and creates a one-day
    /// hello-world calendar event on that user's calendar. Creates nothing when the user cannot
    /// be found in the tenant.
    /// </summary>
    Task<Result> RunHelloWorldTestAsync(
        TeamsGraphCredentials credentials,
        string recipientEmail,
        CancellationToken cancellationToken);
}
