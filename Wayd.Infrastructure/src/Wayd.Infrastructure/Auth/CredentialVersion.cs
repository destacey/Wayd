using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Wayd.Infrastructure.Auth;

/// <summary>
/// A fingerprint of a user's security stamp, which ASP.NET Core Identity replaces whenever a credential changes: a
/// password change or reset, or a password removed. A token or session records the fingerprint it was issued under,
/// and is refused once the user's no longer matches, so a credential change ends every token issued before it.
/// </summary>
/// <remarks>
/// A hash rather than the stamp itself, so a token never carries the value Identity compares.
/// </remarks>
public static class CredentialVersion
{
    /// <summary>The access token claim carrying the fingerprint.</summary>
    public const string ClaimType = "cv";

    /// <summary>The fingerprint of <paramref name="securityStamp"/>: 128 bits of its SHA-256, base64url.</summary>
    public static string Of(string? securityStamp) =>
        Base64UrlEncoder.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(securityStamp ?? string.Empty))[..16]);
}
