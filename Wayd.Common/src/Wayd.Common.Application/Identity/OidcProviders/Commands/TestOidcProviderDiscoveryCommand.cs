namespace Wayd.Common.Application.Identity.OidcProviders.Commands;

/// <summary>
/// Asks the backend to fetch the OIDC discovery document
/// (<c>{authority}/.well-known/openid-configuration</c>) for a configured
/// provider and report whether it returned a usable document. Used by the
/// admin UI's "Test connection" button so operators catch typos in the
/// Authority URL before they cause runtime login failures.
/// </summary>
public sealed record TestOidcProviderDiscoveryCommand(Guid Id) : ICommand<TestOidcProviderDiscoveryResult>;

/// <summary>
/// Outcome of fetching an OIDC provider's discovery document. On success, <c>issuer</c>
/// echoes the value from the discovery document so it can be checked against expectations,
/// and <c>jwksKeyCount</c> is how many signing keys the JWKS endpoint returned (zero
/// indicates a misconfigured provider that can never validate a token). On failure,
/// <c>error</c> says what went wrong.
/// </summary>
public sealed record TestOidcProviderDiscoveryResult(
    bool Success,
    string? Issuer,
    int JwksKeyCount,
    string? Error);
