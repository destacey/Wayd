namespace Wayd.Infrastructure.DataProtection;

/// <summary>
/// Encrypts and decrypts short string secrets (PATs, API keys) for at-rest storage.
///
/// Encoded outputs are tagged with a version prefix so the protector can recognize
/// already-encrypted values and (later) rotate algorithms or keys without ambiguity.
/// </summary>
public interface ISecretProtector
{
    /// <summary>
    /// Encrypt a plaintext secret. Returns a self-describing string that includes
    /// the algorithm version, nonce, and ciphertext. Always encrypts: an input that
    /// already looks protected is treated as plaintext and encrypted again.
    /// </summary>
    string Protect(string plaintext);

    /// <summary>
    /// Decrypt a previously protected secret. Throws if the input is malformed
    /// or authentication fails.
    /// </summary>
    string Unprotect(string protectedValue);

    /// <summary>
    /// True when the input carries the version tag <see cref="Protect"/> writes;
    /// false when it is plaintext (e.g. a legacy value awaiting backfill). Decides
    /// only how a stored value is read: since every write is encrypted, the only
    /// untagged values in storage are ones written before encryption shipped.
    /// </summary>
    bool IsProtected(string value);
}
