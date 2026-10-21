namespace Wayd.Infrastructure.Auth.OAuth;

/// <summary>
/// Decides whether the <c>redirect_uri</c> an authorization request names is one its client registered. A match is
/// exact, except that a loopback redirect may name any port, whether or not it was registered with one (RFC 8252 §7.3,
/// OAuth 2.1 §8.4.2): a desktop client listens on whatever port is free when it signs in.
/// </summary>
/// <remarks>
/// <para>
/// Compared as text rather than through <see cref="Uri"/>, which normalizes paths, case and escapes, so anything that
/// differs from what the client registered other than the port is refused.
/// </para>
/// <para>
/// <c>localhost</c> is included on purpose, although RFC 8252 §8.3 prefers the IP literals: clients such as Claude Code
/// register it, and an exact <c>localhost</c> redirect is accepted anyway.
/// </para>
/// </remarks>
internal static class RedirectUriMatch
{
    private const string LoopbackScheme = "http://";

    /// <summary>The hosts a loopback redirect may name: the IPv4 and IPv6 loopback literals, and <c>localhost</c>.</summary>
    private static readonly string[] _loopbackHosts = ["127.0.0.1", "[::1]", "localhost"];

    /// <summary>Whether <paramref name="requested"/> is one of <paramref name="registered"/>.</summary>
    public static bool IsRegistered(IEnumerable<string> registered, string requested) =>
        registered.Any(uri => Matches(uri, requested));

    /// <summary>
    /// Whether <paramref name="requested"/> matches <paramref name="registered"/>: the same text, or both plain-HTTP
    /// loopback redirects that are the same text apart from the port.
    /// </summary>
    public static bool Matches(string registered, string requested)
    {
        if (string.Equals(registered, requested, StringComparison.Ordinal))
            return true;

        return Loopback(registered) is { } expected
            && Loopback(requested) is { } actual
            && actual.Port is not null
            && string.Equals(expected.Host, actual.Host, StringComparison.Ordinal)
            && string.Equals(expected.Remainder, actual.Remainder, StringComparison.Ordinal);
    }

    /// <summary>
    /// Splits a plain-HTTP loopback redirect into its host, its port (null when it names none) and everything after the
    /// authority. Null for anything else, including an authority with credentials or a port that is not a valid number.
    /// </summary>
    private static (string Host, string? Port, string Remainder)? Loopback(string uri)
    {
        if (!uri.StartsWith(LoopbackScheme, StringComparison.Ordinal))
            return null;

        var afterScheme = uri[LoopbackScheme.Length..];
        var end = afterScheme.IndexOfAny(['/', '?', '#']);
        var authority = end < 0 ? afterScheme : afterScheme[..end];
        var rest = end < 0 ? string.Empty : afterScheme[end..];

        // An IPv6 literal carries colons inside its brackets, so the port separator is the first colon after them.
        var hostEnd = authority.StartsWith('[') ? authority.IndexOf(']') + 1 : 0;
        var colon = authority.IndexOf(':', hostEnd);
        var host = colon < 0 ? authority : authority[..colon];
        var port = colon < 0 ? null : authority[(colon + 1)..];

        if (!_loopbackHosts.Contains(host, StringComparer.Ordinal))
            return null;
        if (port is not null && !IsPort(port))
            return null;

        return (host, port, rest);
    }

    /// <summary>Whether <paramref name="text"/> is a TCP port, 1 to 65535, written as plain digits.</summary>
    private static bool IsPort(string text) =>
        text.Length is > 0 and <= 5
        && text.All(char.IsAsciiDigit)
        && int.Parse(text, System.Globalization.CultureInfo.InvariantCulture) is >= 1 and <= 65535;
}
