using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Wayd.Infrastructure.Auth.OAuth;

/// <summary>Creates and hashes the one-time secrets of an authorization: the consent page's handle and the authorization code.</summary>
internal static class OAuthSecrets
{
    /// <summary>A new 256-bit random value, base64url-encoded so it travels in a URL unescaped.</summary>
    internal static string Create() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// The value's SHA-256 as 64 hex characters. Unsalted, so a stored hash can be found by equality: that is safe
    /// only because every value hashed here is a 256-bit random secret from <see cref="Create"/>.
    /// </summary>
    internal static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
