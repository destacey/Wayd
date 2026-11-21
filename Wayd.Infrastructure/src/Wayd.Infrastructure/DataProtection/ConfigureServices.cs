using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Wayd.Infrastructure.DataProtection;

internal static class ConfigureServices
{
    /// <summary>
    /// SHA-256 of the decoded development key in <c>security.Development.json</c>. That file loads only in
    /// Development, but the key can still reach another host by being copied into its configuration; comparing a
    /// hash catches it wherever it comes from without repeating the key here.
    /// </summary>
    private static readonly byte[] DevelopmentKeyHash =
        Convert.FromHexString("d8f34b7d09183a4e8c134ca5b49b222159908a6d08034165c30b7bb2a4e01633");

    /// <summary>
    /// Registers the at-rest secret protector built from the validated master key and installs it in
    /// <see cref="SecretProtectorAccessor"/>, which the EF value converters read. Must run before persistence is
    /// configured.
    /// </summary>
    internal static IServiceCollection AddDataProtectionForSecrets(
        this IServiceCollection services,
        IConfiguration config,
        IHostEnvironment environment)
    {
        var protector = new AesGcmSecretProtector(ReadMasterKey(config, environment));
        SecretProtectorAccessor.Set(protector);

        services.AddSingleton<ISecretProtector>(protector);
        return services;
    }

    /// <summary>
    /// Reads and validates <c>SecuritySettings:DataProtection:MasterKey</c>, refusing to start when it is missing,
    /// is not a base64 32-byte key, or is the development key from <c>security.Development.json</c> on a host outside
    /// Development, where it would encrypt every connector credential under a key anyone can read.
    /// </summary>
    internal static byte[] ReadMasterKey(IConfiguration config, IHostEnvironment environment)
    {
        var settings = config.GetSection(DataProtectionSettings.SectionName).Get<DataProtectionSettings>()
            ?? throw new InvalidOperationException(
                $"Missing configuration section '{DataProtectionSettings.SectionName}'. " +
                $"Generate a 32-byte key with: openssl rand -base64 32");

        if (string.IsNullOrWhiteSpace(settings.MasterKey))
            throw new InvalidOperationException(
                $"'{DataProtectionSettings.SectionName}:MasterKey' is empty. " +
                $"Generate a 32-byte key with: openssl rand -base64 32");

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(settings.MasterKey);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                $"'{DataProtectionSettings.SectionName}:MasterKey' must be a valid base64 string.", ex);
        }

        if (keyBytes.Length != 32)
            throw new InvalidOperationException(
                $"'{DataProtectionSettings.SectionName}:MasterKey' must decode to exactly 32 bytes (got {keyBytes.Length}).");

        if (!environment.IsDevelopment()
            && CryptographicOperations.FixedTimeEquals(SHA256.HashData(keyBytes), DevelopmentKeyHash))
            throw new InvalidOperationException(
                $"'{DataProtectionSettings.SectionName}:MasterKey' is the development-only key from security.Development.json. " +
                $"Configure a key of your own via user-secrets or environment variables for non-development environments. " +
                $"Generate one with: openssl rand -base64 32");

        return keyBytes;
    }
}
