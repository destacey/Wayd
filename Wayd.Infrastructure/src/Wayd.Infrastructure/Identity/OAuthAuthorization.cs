using NodaTime;

namespace Wayd.Infrastructure.Identity;

/// <summary>
/// One OAuth authorization request to Wayd's authorization server, from the moment a client sends the user to the
/// authorization endpoint until its code is redeemed. It holds what the request asked for, so the consent page is
/// given only an opaque handle and the token endpoint can hold the code to exactly what was approved.
/// </summary>
/// <remarks>
/// The handle and the code are stored only as hashes. Rows are kept for a day after they expire so a code presented
/// twice can be recognised and the session it opened revoked (RFC 6749 §4.1.2).
/// </remarks>
public class OAuthAuthorization
{
    /// <summary>The row's identity.</summary>
    public Guid Id { get; set; }

    /// <summary>SHA-256 of the handle the consent page carries, hex-encoded; the handle itself is never stored.</summary>
    public string RequestHandleHash { get; set; } = null!;

    /// <summary>The client's <c>client_id</c>: the URL of its Client ID Metadata Document.</summary>
    public string ClientId { get; set; } = null!;

    /// <summary>The client's name for itself, cleaned for display when the request arrived. A claim, not a fact.</summary>
    public string ClientName { get; set; } = null!;

    /// <summary>The redirect URI the request named, one of those the client's metadata lists.</summary>
    public string RedirectUri { get; set; } = null!;

    /// <summary>The PKCE challenge (S256) the code's redeemer must answer.</summary>
    public string CodeChallenge { get; set; } = null!;

    /// <summary>The resource (RFC 8707) the issued token is bound to.</summary>
    public string Resource { get; set; } = null!;

    /// <summary>The client's <c>state</c>, returned to it unchanged.</summary>
    public string State { get; set; } = null!;

    /// <summary>The authorization server's issuer when the request arrived, returned as <c>iss</c> (RFC 9207).</summary>
    public string Issuer { get; set; } = null!;

    /// <summary>Where the request stands.</summary>
    public OAuthAuthorizationStatus Status { get; set; }

    /// <summary>When the request arrived.</summary>
    public Instant CreatedAt { get; set; }

    /// <summary>When the request stops waiting for the user's decision.</summary>
    public Instant ExpiresAt { get; set; }

    /// <summary>The user who approved or denied the request.</summary>
    public string? UserId { get; set; }

    /// <summary>When the user approved or denied the request.</summary>
    public Instant? DecidedAt { get; set; }

    /// <summary>SHA-256 of the authorization code, hex-encoded, once the request is approved.</summary>
    public string? CodeHash { get; set; }

    /// <summary>When the authorization code stops being redeemable.</summary>
    public Instant? CodeExpiresAt { get; set; }

    /// <summary>When the code was redeemed.</summary>
    public Instant? RedeemedAt { get; set; }

    /// <summary>The refresh-token session the code opened, revoked if the code is ever presented again.</summary>
    public Guid? SessionId { get; set; }
}

/// <summary>Where an <see cref="OAuthAuthorization"/> stands.</summary>
public enum OAuthAuthorizationStatus
{
    /// <summary>Waiting for the user to approve or deny it.</summary>
    Pending,

    /// <summary>Approved: its code has been issued and not yet redeemed.</summary>
    Approved,

    /// <summary>Denied by the user.</summary>
    Denied,

    /// <summary>Its code has been redeemed for tokens; it can never be redeemed again.</summary>
    Redeemed,
}
