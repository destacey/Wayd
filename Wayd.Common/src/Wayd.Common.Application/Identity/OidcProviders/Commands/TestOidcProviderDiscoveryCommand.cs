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
/// On success: <c>Issuer</c> echoes the value from the discovery document so
/// the admin can sanity-check it matches expectations. <c>JwksKeyCount</c>
/// confirms the JWKS endpoint actually returned signing keys (zero would
/// indicate a misconfigured provider that can never validate a token).
/// </summary>
public sealed record TestOidcProviderDiscoveryResult(
    bool Success,
    string? Issuer,
    int JwksKeyCount,
    string? Error);
