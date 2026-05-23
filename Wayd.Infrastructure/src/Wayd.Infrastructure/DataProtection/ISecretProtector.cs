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
    /// the algorithm version, nonce, and ciphertext.
    /// </summary>
    string Protect(string plaintext);

    /// <summary>
    /// Decrypt a previously protected secret. Throws if the input is malformed
    /// or authentication fails.
    /// </summary>
    string Unprotect(string protectedValue);

    /// <summary>
    /// True when the input was produced by <see cref="Protect"/>; false when it
    /// is plaintext (e.g. a legacy value awaiting backfill). Lets callers
    /// detect un-encrypted values cheaply without attempting decryption.
    /// </summary>
    bool IsProtected(string value);
}
