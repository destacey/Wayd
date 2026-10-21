using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Wayd.Infrastructure.Auth.OAuth;

/// <summary>Proof Key for Code Exchange (RFC 7636) with the S256 method, the only one Wayd accepts.</summary>
internal static partial class Pkce
{
    /// <summary>The only challenge method accepted; <c>plain</c> would let anyone who saw the request redeem the code.</summary>
    internal const string S256 = "S256";

    /// <summary>Whether <paramref name="challenge"/> has the form of an S256 challenge: a base64url SHA-256 hash, 43 characters.</summary>
    internal static bool IsChallenge(string? challenge) => challenge is not null && ChallengePattern().IsMatch(challenge);

    /// <summary>
    /// Whether <paramref name="verifier"/> is well-formed (RFC 7636 §4.1) and hashes to <paramref name="challenge"/>.
    /// The comparison takes the same time however much of the hash matches.
    /// </summary>
    internal static bool Verify(string? verifier, string challenge)
    {
        if (verifier is null || !VerifierPattern().IsMatch(verifier))
            return false;

        var computed = Encoding.ASCII.GetBytes(Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
        return CryptographicOperations.FixedTimeEquals(computed, Encoding.ASCII.GetBytes(challenge));
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{43}$")]
    private static partial Regex ChallengePattern();

    [GeneratedRegex(@"^[A-Za-z0-9\-._~]{43,128}$")]
    private static partial Regex VerifierPattern();
}
