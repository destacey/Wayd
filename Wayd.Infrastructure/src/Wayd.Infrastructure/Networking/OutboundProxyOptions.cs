namespace Wayd.Infrastructure.Networking;

/// <summary>
/// The outbound proxy that requests to addresses typed in as data (<see cref="PublicNetwork"/>) are sent through,
/// for a deployment whose only route to the internet is a proxy.
/// </summary>
public sealed class OutboundProxyOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "OutboundProxy";

    /// <summary>
    /// The proxy's <c>http</c> or <c>https</c> URL, such as <c>http://proxy.corp.example:3128</c>; when unset these
    /// requests connect directly. The proxy itself is trusted and may be on a private address; every destination
    /// is still checked before the request is sent.
    /// </summary>
    public Uri? Address { get; set; }

    /// <summary>Whether <see cref="Address"/> is unset, or an absolute <c>http</c> or <c>https</c> URL that carries no credentials.</summary>
    internal bool IsValid() =>
        Address is null
        || (Address.IsAbsoluteUri
            && (Address.Scheme == Uri.UriSchemeHttp || Address.Scheme == Uri.UriSchemeHttps)
            && string.IsNullOrEmpty(Address.UserInfo));
}
