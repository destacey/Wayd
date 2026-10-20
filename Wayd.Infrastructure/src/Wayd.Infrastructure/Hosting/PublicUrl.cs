using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Wayd.Infrastructure.Hosting;

/// <summary>
/// The address clients reach the API at, for URLs that must not vary with how a request arrived: an OAuth
/// issuer, the resource a token is bound to, and the metadata that names them.
/// </summary>
/// <remarks>
/// Configured by <see cref="ConfigurationKey"/>. Unset, it is read from each request, which is right only
/// where nothing in front of the API rewrites the scheme or host — a TLS-terminating proxy makes every
/// request look like plain HTTP.
/// </remarks>
public sealed class PublicUrl
{
    /// <summary>The configuration key holding the API's public URL, such as <c>https://api.wayd.example.com</c>.</summary>
    public const string ConfigurationKey = "ApiSettings:PublicUrl";

    private readonly string? _configured;

    /// <summary>Creates the public URL from its configured value, throwing when that value is not one Wayd can publish.</summary>
    /// <param name="configured">The configured value, or null or blank to read the URL from each request.</param>
    public PublicUrl(string? configured)
    {
        _configured = Normalize(configured);
    }

    /// <summary>Reads the public URL from <paramref name="configuration"/>.</summary>
    public static PublicUrl From(IConfiguration configuration) => new(configuration[ConfigurationKey]);

    /// <summary>Whether a public URL is configured, rather than read from each request.</summary>
    public bool IsConfigured => _configured is not null;

    /// <summary>The API's public URL, without a trailing slash: the configured one, otherwise the one <paramref name="request"/> arrived at.</summary>
    public string For(HttpRequest request) =>
        _configured ?? $"{request.Scheme}://{request.Host.ToUriComponent()}{request.PathBase}";

    /// <summary>
    /// Returns the value as an ASCII origin, or null when blank. OAuth requires HTTPS for everything it publishes
    /// except on the loopback interface, so a plain-HTTP URL elsewhere is refused rather than published for
    /// clients to reject.
    /// </summary>
    /// <remarks>
    /// A path is refused because the well-known documents an OAuth client looks up sit at the host's root
    /// (RFC 8414, RFC 9728), where a proxy that maps a path prefix onto the API would not send them. An
    /// internationalized host is written in its ASCII form: the URL goes into a response header, which must be ASCII,
    /// and clients compare an issuer as a literal string.
    /// </remarks>
    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http")
            || (uri.Scheme == "http" && !uri.IsLoopback)
            || uri.AbsolutePath != "/"
            || uri.Query.Length > 0
            || uri.Fragment.Length > 0
            || uri.UserInfo.Length > 0)
        {
            throw new InvalidOperationException(
                $"'{ConfigurationKey}' must be an HTTPS origin with no path, query, fragment or credentials, such as "
                + $"https://api.wayd.example.com (HTTP is accepted only for localhost); it is '{value}'.");
        }

        var host = uri.HostNameType == UriHostNameType.IPv6 ? $"[{uri.IdnHost}]" : uri.IdnHost;
        var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
        return $"{uri.Scheme}://{host}{port}";
    }
}
