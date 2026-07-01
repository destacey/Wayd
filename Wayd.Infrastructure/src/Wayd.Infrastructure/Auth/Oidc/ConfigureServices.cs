using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Identity.OidcProviders;

namespace Wayd.Infrastructure.Auth.Oidc;

internal static class ConfigureServices
{
    /// <summary>
    /// Registers the database-backed OIDC provider registry, the per-authority
    /// JWKS <c>ConfigurationManager</c> cache, and the token validator. OIDC
    /// providers are managed entirely through the database (Settings → Identity
    /// Providers); there is no longer any static config to bind here.
    /// </summary>
    internal static IServiceCollection AddOidcProviderRegistry(this IServiceCollection services)
    {
        services.AddSingleton<IOidcProviderRegistry, OidcProviderRegistry>();
        services.AddSingleton<IOidcConfigurationManagerFactory, OidcConfigurationManagerFactory>();

        // Scoped to match the rest of the request-bound auth surface. Its only
        // meaningful state is the logger, but lifetime parity matters because the
        // validator depends on the singleton registry/factory and not the other
        // way around — keeping it scoped avoids accidental upgrade to singleton.
        services.AddScoped<IOidcTokenValidator, OidcTokenValidator>();

        return services;
    }
}
