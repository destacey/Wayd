using System.Security.Claims;

namespace Wayd.Infrastructure.Auth.Oidc;

/// <summary>
/// Validates an OIDC-issued subject token presented to the token-exchange endpoint
/// against the configuration of a registered <c>OidcProvider</c>. Returns the
/// validated principal on success; throws <see cref="UnauthorizedException"/> for
/// any failure (missing/disabled provider, signature/audience/issuer/lifetime
/// failure, tenant not in the Entra allowlist).
/// </summary>
/// <remarks>
/// This is the multi-provider successor to <c>IEntraIdTokenValidator</c>. The
/// caller passes the provider <c>Name</c> (matching <c>OidcProvider.Name</c> and
/// <c>UserIdentity.Provider</c>) along with the subject token; the validator
/// resolves the provider config from the registry, picks the right validation
/// strategy based on <c>ProviderType</c>, and uses the per-authority
/// <c>ConfigurationManager</c> for JWKS.
/// </remarks>
public interface IOidcTokenValidator
{
    Task<ClaimsPrincipal> Validate(string providerName, string subjectToken, CancellationToken cancellationToken);
}
