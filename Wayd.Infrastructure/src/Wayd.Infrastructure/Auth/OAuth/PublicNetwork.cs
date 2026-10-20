using System.Net;
using System.Net.Sockets;

namespace Wayd.Infrastructure.Auth.OAuth;

/// <summary>
/// Outbound HTTP to an address someone outside Wayd chose, such as a client's metadata document, refused for
/// any destination that is not on the public internet, so the URL cannot be used to reach the API's own
/// network (server-side request forgery).
/// </summary>
internal static class PublicNetwork
{
    /// <summary>Ranges that are not publicly routable, or that embed or reach an address that may not be.</summary>
    private static readonly IPNetwork[] _nonPublic =
    [
        IPNetwork.Parse("0.0.0.0/8"),
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("100.64.0.0/10"),
        IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("169.254.0.0/16"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.0.0.0/24"),
        IPNetwork.Parse("192.0.2.0/24"),
        IPNetwork.Parse("192.88.99.0/24"),
        IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("198.18.0.0/15"),
        IPNetwork.Parse("198.51.100.0/24"),
        IPNetwork.Parse("203.0.113.0/24"),
        IPNetwork.Parse("224.0.0.0/4"),
        IPNetwork.Parse("240.0.0.0/4"),
        IPNetwork.Parse("::/96"),
        IPNetwork.Parse("::ffff:0:0:0/96"),
        IPNetwork.Parse("64:ff9b::/96"),
        IPNetwork.Parse("64:ff9b:1::/48"),
        IPNetwork.Parse("100::/64"),
        IPNetwork.Parse("2001::/32"),
        IPNetwork.Parse("2001:20::/28"),
        IPNetwork.Parse("2001:db8::/32"),
        IPNetwork.Parse("2002::/16"),
        IPNetwork.Parse("3fff::/20"),
        IPNetwork.Parse("5f00::/16"),
        IPNetwork.Parse("fc00::/7"),
        IPNetwork.Parse("fe80::/10"),
        IPNetwork.Parse("fec0::/10"),
        IPNetwork.Parse("ff00::/8"),
    ];

    /// <summary>Whether <paramref name="address"/> is publicly routable; an IPv4-mapped IPv6 address is judged as IPv4.</summary>
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        return !_nonPublic.Any(network => network.Contains(address));
    }

    /// <summary>
    /// A handler that connects only to public addresses, follows no redirects, sends no cookies and ignores any
    /// system proxy.
    /// </summary>
    /// <remarks>
    /// The check runs when the connection is made, against the addresses the connection actually uses, so a
    /// host name that resolves to a public address when checked and a private one when connected (DNS rebinding)
    /// is still refused. A proxy would make the connection to the proxy and the check meaningless.
    /// </remarks>
    /// <param name="connectTimeout">How long establishing a connection may take.</param>
    /// <param name="resolve">Resolves a host name to its addresses; DNS unless a test supplies its own.</param>
    public static SocketsHttpHandler CreateHandler(
        TimeSpan connectTimeout,
        Func<string, CancellationToken, Task<IPAddress[]>>? resolve = null)
    {
        resolve ??= Dns.GetHostAddressesAsync;
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            ConnectTimeout = connectTimeout,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectCallback = (context, cancellationToken) => Connect(context, resolve, cancellationToken),
        };
    }

    /// <summary>Connects to the request's host only when every address it resolves to is public.</summary>
    private static async ValueTask<Stream> Connect(
        SocketsHttpConnectionContext context,
        Func<string, CancellationToken, Task<IPAddress[]>> resolve,
        CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await resolve(host, cancellationToken);

        // Every address must be public, not just one: the socket may try any of them.
        if (addresses.Length == 0 || !addresses.All(IsPublic))
            throw new HttpRequestException($"'{host}' does not resolve to a public address.");

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
