namespace Wayd.Infrastructure.Auth.Oidc;

/// <summary>Which networks the API may reach an identity provider's discovery document and signing keys on.</summary>
public sealed class OidcNetworkOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "Oidc";

    /// <summary>
    /// When true, an identity provider's authority may resolve to a private or loopback address, as
    /// an identity provider on the deployment's own network does. Off by default: an administrator types the
    /// authority, and only the host's operator should decide that the API may call into its own network.
    /// </summary>
    public bool AllowPrivateNetworkAuthorities { get; set; }
}
