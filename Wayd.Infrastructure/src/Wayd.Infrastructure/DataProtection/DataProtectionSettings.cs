namespace Wayd.Infrastructure.DataProtection;

public sealed class DataProtectionSettings
{
    public const string SectionName = "SecuritySettings:DataProtection";

    /// <summary>
    /// 256-bit master key for at-rest secret encryption (AES-GCM), base64-encoded.
    ///
    /// Generate with: <c>openssl rand -base64 32</c>
    ///
    /// Must be kept separate from <c>SecuritySettings:LocalJwt:Secret</c>: JWT signing
    /// and data-at-rest encryption have different rotation cadences and threat models
    /// (NIST SP 800-57 §5.2 — a key shall be used for only one purpose).
    /// </summary>
    public string MasterKey { get; set; } = null!;
}
