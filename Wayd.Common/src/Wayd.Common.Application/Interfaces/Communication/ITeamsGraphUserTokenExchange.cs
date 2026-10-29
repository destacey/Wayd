namespace Wayd.Common.Application.Interfaces.Communication;

/// <summary>
/// Exchanges a signed-in user's Entra access token for a Microsoft Graph access token using the
/// Teams communication app registration (on-behalf-of).
/// </summary>
public interface ITeamsGraphUserTokenExchange
{
    /// <summary>
    /// Returns a Graph access token for <paramref name="scopes"/> acting as the user identified by
    /// <paramref name="userAssertion"/>.
    /// </summary>
    Task<Result<string>> ExchangeForGraphAccessTokenAsync(
        TeamsGraphCredentials connectionCredentials,
        string userAssertion,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken);
}
