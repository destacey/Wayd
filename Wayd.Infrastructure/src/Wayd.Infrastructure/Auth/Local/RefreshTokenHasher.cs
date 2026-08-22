using System.Security.Cryptography;
using System.Text;

namespace Wayd.Infrastructure.Auth.Local;

/// <summary>
/// Hashes refresh tokens for storage in <c>Identity.UserRefreshTokens</c>.
/// </summary>
/// <remarks>
/// Deliberately not <c>ITokenHashingService</c>: its <c>TokenIdentifier</c> exists so a
/// personal access token can locate its own row, whereas a refresh token is presented
/// alongside the access token that names the user. Adopting it here would persist a
/// prefix of the secret for no lookup benefit.
///
/// A single salted SHA-256 (no work factor) is sufficient only because the input is a
/// 256-bit CSPRNG value. Never reuse this for a user-chosen secret.
/// </remarks>
internal static class RefreshTokenHasher
{
    private const int SaltBytesLength = 16;

    /// <summary>Hashes a refresh token as <c>{salt}:{hash}</c>, both base64.</summary>
    /// <remarks>~70 characters; the storing column allows 256.</remarks>
    public static string Hash(string token)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytesLength);
        var hash = HashWithSalt(token, salt);

        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Constant-time comparison of a presented token against a stored <c>{salt}:{hash}</c>.
    /// Fails closed on any malformed or absent stored value — a row still holding a
    /// pre-hashing plaintext token must not throw its way onto the auth path.
    /// </summary>
    public static bool Verify(string? token, string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        var parts = storedHash.Split(':');
        if (parts.Length != 2)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[0]);
            expected = Convert.FromBase64String(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expected, HashWithSalt(token, salt));
    }

    private static byte[] HashWithSalt(string token, byte[] salt)
    {
        var tokenBytes = Encoding.UTF8.GetBytes(token);

        var saltedToken = new byte[salt.Length + tokenBytes.Length];
        Buffer.BlockCopy(salt, 0, saltedToken, 0, salt.Length);
        Buffer.BlockCopy(tokenBytes, 0, saltedToken, salt.Length, tokenBytes.Length);

        return SHA256.HashData(saltedToken);
    }
}
