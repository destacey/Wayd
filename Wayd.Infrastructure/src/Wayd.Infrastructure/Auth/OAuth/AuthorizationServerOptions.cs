using Wayd.Infrastructure.Hosting;

namespace Wayd.Infrastructure.Auth.OAuth;

/// <summary>How Wayd's OAuth authorization server is set up by the host that serves it.</summary>
public sealed class AuthorizationServerOptions
{
    /// <summary>
    /// The path of the one protected resource tokens are issued for, such as <c>/mcp</c>. A token's audience, and
    /// the only <c>resource</c> (RFC 8707) a client may ask for, is this path under the API's <see cref="PublicUrl"/>.
    /// </summary>
    public string ResourcePath { get; set; } = null!;

    /// <summary>How long an authorization request waits for the user to sign in and decide.</summary>
    public TimeSpan RequestLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>How long an authorization code can be redeemed; RFC 6749 recommends at most ten minutes, and clients redeem at once.</summary>
    public TimeSpan CodeLifetime { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>The path of the web app's consent page, which the authorization endpoint sends the user to.</summary>
    public string ConsentPath { get; set; } = "/oauth/consent";
}
