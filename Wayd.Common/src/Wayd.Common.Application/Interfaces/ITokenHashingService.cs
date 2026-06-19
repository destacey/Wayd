namespace Wayd.Common.Application.Interfaces;

/// <summary>
/// Service for securely hashing and validating personal access tokens.
/// </summary>
public interface ITokenHashingService
{
    /// <summary>
    /// Generates a cryptographically secure random token.
    /// </summary>
    /// <returns>A URL-safe base64-encoded random token.</returns>
    string GenerateToken();

    /// <summary>
    /// Hashes a token for secure storage.
    /// </summary>
    /// <param name="token">The plain text token to hash.</param>
    /// <returns>A tuple containing the token identifier (first 8 chars) and the hashed token.</returns>
    (string TokenIdentifier, string TokenHash) HashToken(string token);

    /// <summary>
    /// Verifies that a plain text token matches a stored hash.
    /// </summary>
    /// <param name="token">The plain text token to verify.</param>
    /// <param name="tokenIdentifier">The token identifier (first 8 chars) for quick validation.</param>
    /// <param name="hash">The stored hash to verify against.</param>
    /// <returns>True if the token matches the hash, false otherwise.</returns>
    bool VerifyToken(string token, string tokenIdentifier, string hash);
}
