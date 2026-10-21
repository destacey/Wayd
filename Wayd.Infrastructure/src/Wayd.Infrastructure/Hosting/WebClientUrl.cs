using Microsoft.Extensions.Configuration;

namespace Wayd.Infrastructure.Hosting;

/// <summary>
/// The address people reach the web app at, for when the API sends a browser there, as the OAuth authorization
/// endpoint does to ask for consent.
/// </summary>
/// <remarks>
/// Configured by <see cref="ConfigurationKey"/>. Unlike <see cref="PublicUrl"/> it cannot be read from a request,
/// because the web app is served from a different origin than the API.
/// </remarks>
public sealed class WebClientUrl
{
    /// <summary>The configuration key holding the web app's URL, such as <c>https://wayd.example.com</c>.</summary>
    public const string ConfigurationKey = "ApiSettings:WebClientUrl";

    /// <summary>Creates the web app's URL from its configured value, throwing when that value is not an origin Wayd can redirect to.</summary>
    /// <param name="configured">The configured value, or null or blank when the web app's address is not known.</param>
    public WebClientUrl(string? configured)
    {
        Value = Origin.Normalize(configured, ConfigurationKey);
    }

    /// <summary>Reads the web app's URL from <paramref name="configuration"/>.</summary>
    public static WebClientUrl From(IConfiguration configuration) => new(configuration[ConfigurationKey]);

    /// <summary>The web app's origin without a trailing slash, or null when none is configured.</summary>
    public string? Value { get; }
}
