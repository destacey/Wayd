using System.Security.Claims;
using Wayd.Common.Domain.Identity;

namespace Wayd.Infrastructure.Auth.Oidc;

/// <summary>
/// Validates an OIDC-issued subject token presented to the token-exchange endpoint
/// against the configuration of a registered <c>OidcProvider</c>. Returns the
/// validated principal and the stored provider it was validated against on success; throws <see cref="UnauthorizedException"/> for
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
    Task<ValidatedOidcToken> Validate(string providerName, string subjectToken, CancellationToken cancellationToken);
}

/// <summary>
/// A subject token that passed validation, with the stored provider row it passed against.
/// </summary>
/// <param name="Principal">The token's claims.</param>
/// <param name="Provider">
/// The registered provider. Its <c>Name</c> and <c>ProviderType</c> decide the sign-in path and what identity rows
/// record, never the request's spelling of the name: the lookup ignores case, so a request could name a provider in
/// another case and still pass.
/// </param>
public sealed record ValidatedOidcToken(ClaimsPrincipal Principal, OidcProvider Provider);
