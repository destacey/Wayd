using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Infrastructure.Auth.OAuth;

/// <summary>What the authorization endpoint answers a request with.</summary>
public abstract record AuthorizeOutcome
{
    /// <summary>
    /// The request was refused, and the error is shown to the user rather than sent to the client's redirect URI,
    /// which the user has not yet seen or agreed to be sent to (RFC 6749 §4.1.2.1, RFC 9700 §4.11.2).
    /// </summary>
    /// <param name="Description">What was wrong, fit to show a person.</param>
    /// <param name="StatusCode">400 for a bad request, 500 when the server is not set up to answer it.</param>
    public sealed record ShowError(string Description, int StatusCode = 400) : AuthorizeOutcome;

    /// <summary>Send the browser to <paramref name="Location"/>: the web app's consent page.</summary>
    /// <param name="Location">The absolute URL to redirect to.</param>
    public sealed record Redirect(string Location) : AuthorizeOutcome;
}

/// <summary>What the token endpoint answers a request with.</summary>
public abstract record TokenOutcome
{
    /// <summary>Tokens issued (RFC 6749 §5.1).</summary>
    /// <param name="AccessToken">A JWT bound to the protected resource.</param>
    /// <param name="ExpiresIn">Seconds until the access token expires.</param>
    /// <param name="RefreshToken">The session's new refresh token; the one presented, if any, no longer works.</param>
    public sealed record Issued(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("refresh_token")] string RefreshToken) : TokenOutcome
    {
        /// <summary>Always <c>Bearer</c>.</summary>
        [JsonPropertyName("token_type")]
        public string TokenType => "Bearer";
    }

    /// <summary>The request was refused (RFC 6749 §5.2).</summary>
    /// <param name="Error">The RFC's error code, such as <c>invalid_grant</c>.</param>
    /// <param name="Description">A human-readable explanation for the client's developer.</param>
    public sealed record Refused(
        [property: JsonPropertyName("error")] string Error,
        [property: JsonPropertyName("error_description")] string Description) : TokenOutcome;
}

/// <summary>
/// An authorization request waiting for the user's decision, as the consent page shows it. Everything about the
/// client except its <see cref="ClientId"/> and the hosts derived from it is the client's own claim.
/// </summary>
/// <param name="ClientId">The URL of the client's metadata document, which identifies it.</param>
/// <param name="ClientHost">The host <see cref="ClientId"/> is served from: the one fact about who is asking.</param>
/// <param name="ClientName">The client's name for itself, with control and bidi characters removed and its length capped.</param>
/// <param name="RedirectUri">Where the browser goes once the user decides.</param>
/// <param name="RedirectHost">The redirect URI's host, or null when it opens an app by its own scheme.</param>
/// <param name="RedirectHostDiffers">Whether the redirect URI's host is not <see cref="ClientHost"/>.</param>
/// <param name="RedirectIsLoopback">Whether the redirect URI is on this computer's loopback interface, as a native app's is.</param>
/// <param name="ExpiresAt">When the request stops waiting for a decision.</param>
public sealed record PendingAuthorization(
    string ClientId,
    string ClientHost,
    string ClientName,
    string RedirectUri,
    string? RedirectHost,
    bool RedirectHostDiffers,
    bool RedirectIsLoopback,
    Instant ExpiresAt);
