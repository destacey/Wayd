using CSharpFunctionalExtensions;

namespace Wayd.Infrastructure.Auth.OAuth;

/// <summary>Finds the OAuth client a <c>client_id</c> names.</summary>
public interface IOAuthClientResolver
{
    /// <summary>
    /// Resolves <paramref name="clientId"/> to its client, or fails with a reason fit to return to the client as
    /// an <c>invalid_client</c> error description.
    /// </summary>
    Task<Result<OAuthClient>> Resolve(string clientId, CancellationToken cancellationToken);
}
