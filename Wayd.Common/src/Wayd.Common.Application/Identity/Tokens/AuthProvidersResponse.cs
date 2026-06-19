namespace Wayd.Common.Application.Identity.Tokens;

/// <summary>
/// Advertises which authentication options are enabled on this deployment.
/// Returned by <c>GET /api/auth/providers</c> so the unauthenticated frontend
/// can render the right login UI and construct the right OIDC client per
/// provider. Anonymous and cheap.
/// </summary>
/// <remarks>
/// No secrets in this response — <c>ClientId</c> is the public OAuth client
/// identifier (always client-visible in any OIDC SPA flow), and
/// <c>AllowedTenantIds</c> is intentionally NOT exposed because it's a
/// security gate. Server-side validation enforces it; the frontend never
/// needs to know.
/// </remarks>
public sealed record AuthProvidersResponse(
    bool Local,
    IReadOnlyList<OidcProviderInfo> Oidc);

/// <summary>
/// Public-facing metadata for one registered OIDC provider. Exactly the fields
/// a generic OIDC client (oidc-client-ts, etc.) needs to bootstrap a sign-in.
/// </summary>
public sealed record OidcProviderInfo(
    string Name,
    string DisplayName,
    string ProviderType,
    string Authority,
    string ClientId,
    IReadOnlyList<string> Scopes);
