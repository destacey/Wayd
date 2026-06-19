using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Wayd.Infrastructure.Auth.Oidc;

/// <summary>
/// Returns the <see cref="IConfigurationManager{T}"/> for a given OIDC authority.
/// Implementations are expected to hold one long-lived instance per distinct
/// authority URL — the underlying ConfigurationManager caches the JWKS and
/// auto-refreshes on its own schedule, so rebuilding it per request would defeat
/// the caching that's the whole point of using it.
/// </summary>
public interface IOidcConfigurationManagerFactory
{
    IConfigurationManager<OpenIdConnectConfiguration> Get(string authority);
}
