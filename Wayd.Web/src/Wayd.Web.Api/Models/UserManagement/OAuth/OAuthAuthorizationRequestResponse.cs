using NodaTime;
using Wayd.Infrastructure.Auth.OAuth;

namespace Wayd.Web.Api.Models.UserManagement.OAuth;

/// <summary>
/// An application asking to connect to Wayd as the signed-in user, as the consent page shows it. Only `clientId` and
/// the hosts derived from it are facts; the name is the application's own claim about itself.
/// </summary>
public sealed record OAuthAuthorizationRequestResponse
{
    /// <summary>The URL of the application's metadata document, which identifies it.</summary>
    public required string ClientId { get; init; }

    /// <summary>The host the application's metadata document is served from: the one fact about who is asking. Show it prominently.</summary>
    public required string ClientHost { get; init; }

    /// <summary>The application's name for itself, with control and bidirectional-override characters removed and its length capped.</summary>
    public required string ClientName { get; init; }

    /// <summary>Where the browser goes once the user approves or denies.</summary>
    public required string RedirectUri { get; init; }

    /// <summary>The redirect URI's host, or null when it opens an installed app by its own URL scheme.</summary>
    public string? RedirectHost { get; init; }

    /// <summary>Whether the redirect URI's host is not the client's host; the consent page warns when it is not.</summary>
    public required bool RedirectHostDiffers { get; init; }

    /// <summary>Whether the redirect URI is on this computer's loopback interface, as a desktop app's is.</summary>
    public required bool RedirectIsLoopback { get; init; }

    /// <summary>When the request stops waiting for a decision.</summary>
    public required Instant ExpiresAt { get; init; }

    /// <summary>Maps the authorization server's view of a pending request.</summary>
    internal static OAuthAuthorizationRequestResponse From(PendingAuthorization pending) => new()
    {
        ClientId = pending.ClientId,
        ClientHost = pending.ClientHost,
        ClientName = pending.ClientName,
        RedirectUri = pending.RedirectUri,
        RedirectHost = pending.RedirectHost,
        RedirectHostDiffers = pending.RedirectHostDiffers,
        RedirectIsLoopback = pending.RedirectIsLoopback,
        ExpiresAt = pending.ExpiresAt,
    };
}

/// <summary>Where to send the browser after the user approves or denies a connection request.</summary>
public sealed record OAuthAuthorizationDecisionResponse
{
    /// <summary>The application's redirect URI, carrying the authorization code or the refusal, `state` and `iss`.</summary>
    public required string RedirectUrl { get; init; }
}
