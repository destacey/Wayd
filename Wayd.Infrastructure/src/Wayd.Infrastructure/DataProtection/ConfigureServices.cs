using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Wayd.Infrastructure.DataProtection;

internal static class ConfigureServices
{
    internal static IServiceCollection AddDataProtectionForSecrets(
        this IServiceCollection services,
        IConfiguration config)
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

        var protector = new AesGcmSecretProtector(keyBytes);
        SecretProtectorAccessor.Set(protector);

        services.AddSingleton<ISecretProtector>(protector);
        return services;
    }
}
