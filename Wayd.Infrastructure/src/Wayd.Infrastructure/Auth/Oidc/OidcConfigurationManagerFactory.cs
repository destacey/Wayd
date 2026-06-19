using System.Collections.Concurrent;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Wayd.Infrastructure.Auth.Oidc;

/// <summary>
/// Lazy, process-wide cache of <see cref="ConfigurationManager{T}"/> instances
/// keyed by authority URL. Replaces the single-singleton registration the
/// Entra-only flow used: we now have N providers, each with its own authority,
/// each needing its own long-lived ConfigurationManager so JWKS + discovery
/// fetches stay cached.
/// </summary>
/// <remarks>
/// Normalization: authority URLs are stored without trailing slashes and with
/// a lowercase scheme/host so trivial variants resolve to the same instance
/// ("https://login.microsoftonline.com/common/v2.0" vs the trailing-slash form).
/// The downstream <c>OpenIdConnectConfigurationRetriever</c> tolerates either,
/// but a one-instance-per-URL-variant policy would silently double the cache
/// footprint and JWKS fetch rate.
/// </remarks>
internal sealed class OidcConfigurationManagerFactory : IOidcConfigurationManagerFactory
{
    // Ordinal (case-sensitive) on purpose: scheme and host are lowercased during
    // normalization, but URL paths are case-sensitive per RFC 3986. Using an
    // OrdinalIgnoreCase comparer here would silently route /tenants/abc and
    // /tenants/ABC to the same ConfigurationManager — wrong for any provider
    // that uses case-significant path segments.
    private readonly ConcurrentDictionary<string, IConfigurationManager<OpenIdConnectConfiguration>> _managers =
        new(StringComparer.Ordinal);

    public IConfigurationManager<OpenIdConnectConfiguration> Get(string authority)
    {
        if (string.IsNullOrWhiteSpace(authority))
        {
            throw new ArgumentException("Authority must be provided.", nameof(authority));
        }

        var key = NormalizeAuthority(authority);

        return _managers.GetOrAdd(key, normalizedAuthority =>
            new ConfigurationManager<OpenIdConnectConfiguration>(
                $"{normalizedAuthority}/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(),
                // RequireHttps mirrors the entity-level Authority invariant.
                // Defense-in-depth: even if a malformed row somehow reached the
                // factory, the retriever refuses non-HTTPS.
                new HttpDocumentRetriever { RequireHttps = true }));
    }

    private static string NormalizeAuthority(string authority)
    {
        var trimmed = authority.Trim().TrimEnd('/');

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            // The entity guarantees a valid HTTPS URL on writes, but the factory
            // is a runtime boundary; falling through with the raw string makes
            // the failure surface at JWKS fetch time, which is a worse error
            // experience than failing here with the authority value in scope.
            throw new ArgumentException($"Authority is not a valid absolute URL: {authority}", nameof(authority));
        }

        // Rebuild with lowercase scheme/host. Path is case-sensitive per RFC and
        // some Entra-like providers use case-significant path segments (rare,
        // but possible) — leave it alone.
        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.Host.ToLowerInvariant(),
        };
        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }
}
