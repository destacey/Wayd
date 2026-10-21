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
        _configured = Origin.Normalize(configured, ConfigurationKey);
    }

    /// <summary>Reads the public URL from <paramref name="configuration"/>.</summary>
    public static PublicUrl From(IConfiguration configuration) => new(configuration[ConfigurationKey]);

    /// <summary>Whether a public URL is configured, rather than read from each request.</summary>
    public bool IsConfigured => _configured is not null;

    /// <summary>The API's public URL, without a trailing slash: the configured one, otherwise the one <paramref name="request"/> arrived at.</summary>
    public string For(HttpRequest request) =>
        _configured ?? $"{request.Scheme}://{request.Host.ToUriComponent()}{request.PathBase}";
}
