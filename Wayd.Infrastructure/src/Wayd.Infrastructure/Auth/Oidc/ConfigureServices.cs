using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wayd.Common.Application.Identity.OidcProviders;
using Wayd.Infrastructure.Networking;

namespace Wayd.Infrastructure.Auth.Oidc;

internal static class ConfigureServices
{
    /// <summary>The longest one discovery document or key set fetch may take, connection included.</summary>
    private static readonly TimeSpan _fetchTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The largest discovery document or key set accepted. Real ones are a few kilobytes; without a cap the
    /// client buffers up to 2 GB, which any public authority could stream on every test or sign-in.
    /// </summary>
    internal const int MaxResponseBytes = 1024 * 1024;

    /// <summary>
    /// Registers the database-backed OIDC provider registry, the per-authority
    /// JWKS <c>ConfigurationManager</c> cache, and the token validator. OIDC
    /// providers are managed entirely through the database (Settings → Identity
    /// Providers); there is no longer any static config to bind here.
    /// </summary>
    /// <remarks>
    /// The authority is typed by an administrator, so the factory's client reaches only public addresses
    /// (<see cref="PublicNetwork"/>), and private ones too only when the operator sets
    /// <see cref="OidcNetworkOptions.AllowPrivateNetworkAuthorities"/>; link-local addresses, where cloud metadata
    /// lives, are refused either way. It is built here rather than taken from the HTTP client factory, whose
    /// defaults add service discovery and retries, and follows no redirect, so a permitted authority cannot
    /// bounce the fetch to an address it would not be allowed to name.
    /// </remarks>
    internal static IServiceCollection AddOidcProviderRegistry(this IServiceCollection services)
    {
        services.AddOptions<OidcNetworkOptions>().BindConfiguration(OidcNetworkOptions.SectionName);

        services.AddSingleton<IOidcProviderRegistry, OidcProviderRegistry>();
        services.AddSingleton<IOidcConfigurationManagerFactory>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<OidcNetworkOptions>>().Value;
            var handler = options.AllowPrivateNetworkAuthorities
                ? PublicNetwork.CreatePrivateNetworkHandler(_fetchTimeout)
                : PublicNetwork.CreateHandler(_fetchTimeout);

            return new OidcConfigurationManagerFactory(CreateClient(handler));
        });

        // Scoped to match the rest of the request-bound auth surface. Its only
        // meaningful state is the logger, but lifetime parity matters because the
        // validator depends on the singleton registry/factory and not the other
        // way around — keeping it scoped avoids accidental upgrade to singleton.
        services.AddScoped<IOidcTokenValidator, OidcTokenValidator>();

        return services;
    }

    /// <summary>The client every discovery document and key set is fetched with, bounded in time and size.</summary>
    internal static HttpClient CreateClient(HttpMessageHandler handler) =>
        new(handler) { Timeout = _fetchTimeout, MaxResponseContentBufferSize = MaxResponseBytes };
}
