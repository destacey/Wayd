using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Http;

namespace Wayd.Infrastructure.Auth.OAuth;

/// <summary>
/// Wayd's OAuth authorization server: the authorization code grant with PKCE for public clients identified by their
/// Client ID Metadata Documents, and the refresh token grant.
/// </summary>
public interface IAuthorizationServer
{
    /// <summary>
    /// Handles a request to the authorization endpoint: validates it, records it, and sends the user to the web app's
    /// consent page with a handle for it.
    /// </summary>
    Task<AuthorizeOutcome> Authorize(HttpRequest request, CancellationToken cancellationToken);

    /// <summary>Finds the request <paramref name="handle"/> names, failing when it is unknown, expired, or already decided.</summary>
    Task<Result<PendingAuthorization>> FindPending(string handle, CancellationToken cancellationToken);

    /// <summary>
    /// Approves the request for <paramref name="userId"/>, granting <paramref name="scope"/>, issuing a single-use
    /// authorization code, and answers the URL the browser goes to next. A scope wider than the client asked for is
    /// narrowed to what it asked for: the user can only narrow a request. Fails as <see cref="FindPending"/> does.
    /// </summary>
    Task<Result<string>> Approve(string handle, string userId, TokenScope scope, CancellationToken cancellationToken);

    /// <summary>Denies the request and answers the URL that tells the client so. Fails as <see cref="FindPending"/> does.</summary>
    Task<Result<string>> Deny(string handle, string userId, CancellationToken cancellationToken);

    /// <summary>Handles a request to the token endpoint, which must be form-encoded.</summary>
    Task<TokenOutcome> Token(HttpRequest request, CancellationToken cancellationToken);
}
