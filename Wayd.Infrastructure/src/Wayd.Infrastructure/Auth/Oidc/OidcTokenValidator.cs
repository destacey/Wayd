using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Wayd.Common.Application.Identity.OidcProviders;
using Wayd.Common.Domain.Identity;

namespace Wayd.Infrastructure.Auth.Oidc;

/// <summary>
/// Validates OIDC subject tokens against a database-backed <see cref="OidcProvider"/>
/// configuration. Generalizes the original Entra-only <c>EntraIdTokenValidator</c>:
/// signature validation, audience pinning, lifetime, and algorithm pinning are
/// identical across providers; what differs is the issuer/tenant check, driven
/// by <see cref="OidcProviderType"/>.
/// </summary>
internal sealed class OidcTokenValidator : IOidcTokenValidator
{
    private readonly IOidcProviderRegistry _registry;
    private readonly IOidcConfigurationManagerFactory _configManagerFactory;
    private readonly ILogger<OidcTokenValidator> _logger;

    public OidcTokenValidator(
        IOidcProviderRegistry registry,
        IOidcConfigurationManagerFactory configManagerFactory,
        ILogger<OidcTokenValidator> logger)
    {
        _registry = registry;
        _configManagerFactory = configManagerFactory;
        _logger = logger;
    }

    public async Task<ClaimsPrincipal> Validate(string providerName, string subjectToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providerName) || string.IsNullOrWhiteSpace(subjectToken))
        {
            throw new UnauthorizedException("Invalid token.");
        }

        var provider = await _registry.GetByName(providerName, cancellationToken);
        if (provider is null)
        {
            // The same generic "Invalid token" response as every other rejection
            // path — don't leak whether the provider name was known but disabled
            // vs unknown entirely. Log details for operators.
            _logger.LogWarning("Token exchange rejected: unknown provider '{Provider}'.", providerName);
            throw new UnauthorizedException("Invalid token.");
        }

        if (!provider.IsEnabled)
        {
            _logger.LogWarning("Token exchange rejected: provider '{Provider}' is disabled.", providerName);
            throw new UnauthorizedException("Invalid token.");
        }

        OpenIdConnectConfiguration oidcConfig;
        try
        {
            var configManager = _configManagerFactory.Get(provider.Authority);
            oidcConfig = await configManager.GetConfigurationAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch OIDC discovery for provider '{Provider}' at {Authority}.",
                provider.Name, provider.Authority);
            throw new UnauthorizedException("Invalid token.");
        }

        var validationParameters = BuildValidationParameters(provider, oidcConfig);

        ClaimsPrincipal principal;
        try
        {
            // MapInboundClaims = false keeps JWT short names (tid, iss, sub) on
            // the principal. The default map rewrites them to long schema URLs,
            // which would break the tid/iss lookups for the Entra path. Short
            // names are the OIDC standard.
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            principal = handler.ValidateToken(subjectToken, validationParameters, out _);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            _logger.LogWarning(ex, "Token validation failed for provider '{Provider}'.", provider.Name);
            throw new UnauthorizedException("Invalid token.");
        }

        // ProviderType drives the post-signature checks that differ between Entra
        // (which uses /common/ + a tenant allowlist on the tid claim) and any
        // standards-compliant single-issuer OIDC provider (where issuer match is
        // already enforced by ValidationParameters).
        if (provider.ProviderType == OidcProviderType.MicrosoftEntraId)
        {
            EnforceEntraTenantAllowlist(provider, principal);
        }

        return principal;
    }

    private static TokenValidationParameters BuildValidationParameters(
        OidcProvider provider,
        OpenIdConnectConfiguration oidcConfig)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = oidcConfig.SigningKeys,

            ValidateAudience = true,
            ValidAudience = provider.Audience,

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(provider.ClockSkewSeconds),

            // Pin the algorithm to RS256. All major OIDC providers use it; accepting
            // "none" or HS256 would be a classic alg-confusion hole even with an
            // IssuerSigningKey set, because some libraries happily use the public
            // key as an HMAC secret if asked.
            ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 },
        };

        if (provider.ProviderType == OidcProviderType.MicrosoftEntraId)
        {
            // Entra's /common/ endpoint issues tokens whose `iss` includes the
            // user's tenant GUID in the path — different per tenant. Validating
            // issuer against a single string here would reject every legitimate
            // token. The trust comes from the JWKS signature plus the explicit
            // tenant allowlist applied after signature validation.
            parameters.ValidateIssuer = false;
        }
        else
        {
            // GenericOidc: standards-compliant providers have a single, stable
            // issuer value (usually the discovery document's "issuer" field).
            // Pin to it so a malicious token from a different IdP with a stolen
            // audience can't slip through.
            parameters.ValidateIssuer = true;
            parameters.ValidIssuer = oidcConfig.Issuer;
        }

        return parameters;
    }

    private void EnforceEntraTenantAllowlist(OidcProvider provider, ClaimsPrincipal principal)
    {
        // The entity rejects an Entra row created without AllowedTenantIds, but
        // a row could theoretically be in the DB pre-invariant (defensive null
        // check) or mutated externally. Empty allowlist means reject everything.
        var allowed = provider.AllowedTenantIds;
        if (allowed is null || allowed.Count == 0)
        {
            _logger.LogWarning(
                "Entra provider '{Provider}' has empty AllowedTenantIds at runtime; rejecting exchange.",
                provider.Name);
            throw new UnauthorizedException("Invalid token.");
        }

        // Prefer the `tid` claim (standard on org-context tokens) but fall back
        // to extracting the tenant from `iss` when tid is absent. Personal
        // accounts as guests and some v2.0 edge cases omit tid. The issuer is
        // signed, so deriving the tenant from it is just as trustworthy as tid.
        var tid =
            principal.FindFirstValue("tid")
            ?? ExtractTenantFromIssuer(principal.FindFirstValue("iss"));

        if (string.IsNullOrWhiteSpace(tid) ||
            !allowed.Contains(tid, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Entra token rejected for provider '{Provider}': tenant {Tid} not in allowlist.",
                provider.Name, tid ?? "<missing>");
            throw new UnauthorizedException("Invalid token.");
        }
    }

    /// <summary>
    /// Parses the tenant GUID from an Entra issuer URL. Handles both v1 and v2
    /// shapes:
    ///   v1: https://sts.windows.net/{tid}/
    ///   v2: https://login.microsoftonline.com/{tid}/v2.0
    /// Returns the first path segment if it parses as a GUID, otherwise null.
    /// <para>
    /// This is a shape check, not a trust decision — the MSA (personal accounts)
    /// pseudo-tenant <c>9188040d-6c67-4c5b-b112-36a304b66dad</c> is a real GUID
    /// and <em>is</em> returned. Keeping personal accounts out is the
    /// allowlist's job, not this function's. Non-GUID path segments like
    /// <c>common</c> and <c>organizations</c> return null because they aren't
    /// tenant IDs in the first place.
    /// </para>
    /// </summary>
    internal static string? ExtractTenantFromIssuer(string? issuer)
    {
        if (string.IsNullOrWhiteSpace(issuer)) return null;
        if (!Uri.TryCreate(issuer, UriKind.Absolute, out var uri)) return null;

        var segments = uri.Segments
            .Select(s => s.Trim('/'))
            .Where(s => s.Length > 0)
            .ToArray();
        if (segments.Length == 0) return null;

        var candidate = segments[0];
        return Guid.TryParse(candidate, out _) ? candidate : null;
    }
}
