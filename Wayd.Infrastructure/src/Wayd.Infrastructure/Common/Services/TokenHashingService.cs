using System.Security.Cryptography;
using System.Text;

namespace Wayd.Infrastructure.Common.Services;

/// <summary>
/// Implementation of token hashing service using SHA256 with salt.
/// </summary>
public class TokenHashingService : ITokenHashingService
{
    private const int TokenBytesLength = 32; // 32 bytes = 256 bits of randomness
    private const int SaltBytesLength = 16; // 16 bytes = 128 bits salt
    private const int TokenIdentifierLength = 8; // First 8 characters used as identifier

    /// <summary>
    /// Generates a cryptographically secure random token.
    /// </summary>
    /// <returns>A URL-safe base64-encoded random token.</returns>
    public string GenerateToken()
    {
        byte[] tokenBytes = RandomNumberGenerator.GetBytes(TokenBytesLength);
        string tokenValue = Convert.ToBase64String(tokenBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('='); // URL-safe base64

        return tokenValue;
    }

    /// <summary>
    /// Hashes a token for secure storage using SHA256 with a random salt.
    /// </summary>
    /// <param name="token">The plain text token to hash.</param>
    /// <returns>A tuple containing the token identifier (first 8 chars) and the hash with salt (format: {salt}:{hash}).</returns>
    public (string TokenIdentifier, string TokenHash) HashToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token cannot be null or empty.", nameof(token));
        }

        // Extract identifier (first 8 characters)
        string tokenIdentifier = token.Length >= TokenIdentifierLength
            ? token.Substring(0, TokenIdentifierLength)
            : token;

        // Generate random salt
        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytesLength);

        // Hash the token with the salt
        byte[] hash = HashWithSalt(token, salt);

        // Return tuple with identifier and hash
        string tokenHash = $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
        return (tokenIdentifier, tokenHash);
    }

    /// <summary>
    /// Verifies that a plain text token matches a stored hash.
    /// </summary>
    /// <param name="token">The plain text token to verify.</param>
    /// <param name="tokenIdentifier">The stored token identifier for quick validation.</param>
    /// <param name="hash">The stored hash to verify against (format: {salt}:{hash}).</param>
    /// <returns>True if the token matches the hash, false otherwise.</returns>
    public bool VerifyToken(string token, string tokenIdentifier, string hash)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        // Quick check: does the token start with the expected identifier?
        if (!token.StartsWith(tokenIdentifier, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            // Split the stored hash into salt and hash parts
            string[] parts = hash.Split(':');
            if (parts.Length != 2)
            {
                return false;
            }

            byte[] salt = Convert.FromBase64String(parts[0]);
            byte[] storedHash = Convert.FromBase64String(parts[1]);

            // Hash the provided token with the stored salt
            byte[] computedHash = HashWithSalt(token, salt);

            // Compare the hashes using a constant-time comparison
            return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Hashes a token with a given salt using SHA256.
    /// </summary>
    private static byte[] HashWithSalt(string token, byte[] salt)
    {
        byte[] tokenBytes = Encoding.UTF8.GetBytes(token);

        // Combine salt and token bytes
        byte[] saltedToken = new byte[salt.Length + tokenBytes.Length];
        Buffer.BlockCopy(salt, 0, saltedToken, 0, salt.Length);
        Buffer.BlockCopy(tokenBytes, 0, saltedToken, salt.Length, tokenBytes.Length);

        // Hash with SHA256
        return SHA256.HashData(saltedToken);
    }
}
