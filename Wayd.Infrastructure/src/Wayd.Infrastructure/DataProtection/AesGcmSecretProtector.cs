using System.Security.Cryptography;
using System.Text;

namespace Wayd.Infrastructure.DataProtection;

/// <summary>
/// AES-256-GCM secret protector.
///
/// Output format (base64-encoded):  <c>wayd1:{nonce}:{ciphertext_with_tag}</c>
/// — <c>wayd1</c> is the algorithm/version tag so the format can evolve later
///   (e.g. a future <c>wayd2</c> using a different KDF or AEAD) while still
///   recognizing legacy ciphertexts.
/// </summary>
internal sealed class AesGcmSecretProtector : ISecretProtector
{
    private const string VersionTag = "wayd1";
    private const int NonceSize = 12; // 96-bit nonce — AES-GCM standard
    private const int TagSize = 16;   // 128-bit auth tag

    private readonly byte[] _key;

    public AesGcmSecretProtector(byte[] key)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (key.Length != 32)
            throw new ArgumentException("Master key must be 32 bytes (256 bits).", nameof(key));
        _key = key;
    }

    public bool IsProtected(string value)
        => value is not null && value.StartsWith(VersionTag + ":", StringComparison.Ordinal);

    public string Protect(string plaintext)
    {
        if (plaintext is null) throw new ArgumentNullException(nameof(plaintext));
        if (IsProtected(plaintext)) return plaintext; // idempotent

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var combined = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, combined, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, ciphertext.Length, tag.Length);

        return $"{VersionTag}:{Convert.ToBase64String(nonce)}:{Convert.ToBase64String(combined)}";
    }

    public string Unprotect(string protectedValue)
    {
        if (protectedValue is null) throw new ArgumentNullException(nameof(protectedValue));
        if (!IsProtected(protectedValue))
            throw new FormatException("Value is not an encrypted secret.");

        var parts = protectedValue.Split(':');
        if (parts.Length != 3)
            throw new FormatException("Encrypted secret has unexpected structure.");

        var nonce = Convert.FromBase64String(parts[1]);
        var combined = Convert.FromBase64String(parts[2]);
        if (combined.Length < TagSize)
            throw new FormatException("Encrypted secret payload is too short.");

        var ciphertext = new byte[combined.Length - TagSize];
        var tag = new byte[TagSize];
        Buffer.BlockCopy(combined, 0, ciphertext, 0, ciphertext.Length);
        Buffer.BlockCopy(combined, ciphertext.Length, tag, 0, TagSize);

        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
