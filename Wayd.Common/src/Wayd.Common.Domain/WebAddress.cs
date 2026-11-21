namespace Wayd.Common.Domain;

/// <summary>
/// The rule for a URL a user supplies that Wayd stores and later serves as a link.
/// </summary>
/// <remarks>
/// The web client refuses dangerous schemes when it renders a link, but the same values reach MCP clients,
/// exports and imports, which do not, so the server stores only addresses that are safe to open anywhere.
/// </remarks>
public static class WebAddress
{
    /// <summary>
    /// Whether the text, trimmed, is an absolute <c>http</c> or <c>https</c> address. A relative path, a bare
    /// host and any other scheme (<c>javascript:</c>, <c>data:</c>, <c>file:</c>) are not.
    /// </summary>
    public static bool IsHttp(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
