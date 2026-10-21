namespace Wayd.Infrastructure.Hosting;

/// <summary>Validates a configured origin that Wayd publishes or sends browsers to, such as <see cref="PublicUrl"/>.</summary>
internal static class Origin
{
    /// <summary>
    /// Returns the value as an ASCII origin, or null when blank. OAuth requires HTTPS for everything it publishes
    /// except on the loopback interface, so a plain-HTTP URL elsewhere is refused rather than published for
    /// clients to reject.
    /// </summary>
    /// <param name="value">The configured value.</param>
    /// <param name="configurationKey">The key the value was read from, named in the error.</param>
    /// <remarks>
    /// A path is refused because the well-known documents an OAuth client looks up sit at the host's root
    /// (RFC 8414, RFC 9728), where a proxy that maps a path prefix onto the API would not send them. An
    /// internationalized host is written in its ASCII form: the URL goes into a response header, which must be ASCII,
    /// and clients compare an issuer as a literal string.
    /// </remarks>
    internal static string? Normalize(string? value, string configurationKey)
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
                $"'{configurationKey}' must be an HTTPS origin with no path, query, fragment or credentials, such as "
                + $"https://wayd.example.com (HTTP is accepted only for localhost); it is '{value}'.");
        }

        var host = uri.HostNameType == UriHostNameType.IPv6 ? $"[{uri.IdnHost}]" : uri.IdnHost;
        var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
        return $"{uri.Scheme}://{host}{port}";
    }
}
