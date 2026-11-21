using System.Net;
using System.Net.Sockets;

namespace Wayd.Infrastructure.Networking;

/// <summary>
/// Outbound HTTP to an address that comes from data rather than from the host's configuration — a client's
/// metadata document, an administrator's connection or identity provider — refused for any destination that is
/// not on the public internet, so the URL cannot be used to reach the API's own network (server-side request
/// forgery).
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

    /// <summary>
    /// Ranges refused even when private networks are allowed: link-local, Alibaba Cloud's 100.100.100.200 and
    /// AWS's IPv6 address hold cloud instance metadata and its credentials, the unspecified range reaches the host
    /// itself, and multicast is never an HTTP server. The local-use NAT64 prefix and Teredo embed an IPv4 address
    /// in a form that cannot be judged, so they are refused whole.
    /// </summary>
    private static readonly IPNetwork[] _neverReachable =
    [
        IPNetwork.Parse("0.0.0.0/8"),
        IPNetwork.Parse("100.100.100.200/32"),
        IPNetwork.Parse("169.254.0.0/16"),
        IPNetwork.Parse("224.0.0.0/4"),
        IPNetwork.Parse("::/128"),
        IPNetwork.Parse("64:ff9b:1::/48"),
        IPNetwork.Parse("2001::/32"),
        IPNetwork.Parse("fd00:ec2::254/128"),
        IPNetwork.Parse("fe80::/10"),
        IPNetwork.Parse("ff00::/8"),
    ];

    private static readonly IPNetwork _nat64 = IPNetwork.Parse("64:ff9b::/96");
    private static readonly IPNetwork _ipv4Compatible = IPNetwork.Parse("::/96");
    private static readonly IPNetwork _ipv4Translated = IPNetwork.Parse("::ffff:0:0:0/96");
    private static readonly IPNetwork _sixToFour = IPNetwork.Parse("2002::/16");

    /// <summary>Whether <paramref name="address"/> is publicly routable; an IPv4-mapped IPv6 address is judged as IPv4.</summary>
    public static bool IsPublic(IPAddress address) => !InAny(_nonPublic, address);

    /// <summary>
    /// Whether <paramref name="address"/> may be reached when the operator allows private networks: anything but
    /// cloud metadata, unspecified and multicast addresses, however an IPv6 address embeds them.
    /// </summary>
    public static bool IsReachableOnPrivateNetwork(IPAddress address) =>
        !InAny(_neverReachable, address)
        && (EmbeddedIPv4(address) is not { } embedded || !InAny(_neverReachable, embedded));

    /// <summary>
    /// The IPv4 address a NAT64, IPv4-translated, IPv4-compatible or 6to4 address reaches, which a gateway may route to as readily as
    /// to the address itself; null for any other address. <c>::1</c> and <c>::</c> embed nothing.
    /// </summary>
    private static IPAddress? EmbeddedIPv4(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetworkV6 || address.IsIPv4MappedToIPv6)
            return null;

        var bytes = address.GetAddressBytes();
        if (_nat64.Contains(address) || _ipv4Translated.Contains(address))
            return new IPAddress(bytes[12..16]);
        if (_ipv4Compatible.Contains(address))
            return bytes[12] == 0 ? null : new IPAddress(bytes[12..16]);
        if (_sixToFour.Contains(address))
            return new IPAddress(bytes[2..6]);

        return null;
    }

    private static bool InAny(IPNetwork[] networks, IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        return networks.Any(network => network.Contains(address));
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
        Func<string, CancellationToken, Task<IPAddress[]>>? resolve = null) =>
        CreateHandler(connectTimeout, IsPublic, "a public address", resolve);

    /// <summary>
    /// <see cref="CreateHandler(TimeSpan, Func{string, CancellationToken, Task{IPAddress[]}}?)"/> for a destination the
    /// operator has allowed on the deployment's own network: private and loopback addresses are reached too, but
    /// never <see cref="IsReachableOnPrivateNetwork">cloud metadata, unspecified or multicast</see> ones.
    /// </summary>
    public static SocketsHttpHandler CreatePrivateNetworkHandler(
        TimeSpan connectTimeout,
        Func<string, CancellationToken, Task<IPAddress[]>>? resolve = null) =>
        CreateHandler(connectTimeout, IsReachableOnPrivateNetwork, "an allowed address", resolve);

    private static SocketsHttpHandler CreateHandler(
        TimeSpan connectTimeout,
        Func<IPAddress, bool> isAllowed,
        string allowedDescription,
        Func<string, CancellationToken, Task<IPAddress[]>>? resolve)
    {
        resolve ??= Dns.GetHostAddressesAsync;
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            ConnectTimeout = connectTimeout,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectCallback = (context, cancellationToken) => Connect(context, resolve, isAllowed, allowedDescription, cancellationToken),
        };
    }

    /// <summary>Connects to the request's host only when every address it resolves to is allowed.</summary>
    private static async ValueTask<Stream> Connect(
        SocketsHttpConnectionContext context,
        Func<string, CancellationToken, Task<IPAddress[]>> resolve,
        Func<IPAddress, bool> isAllowed,
        string allowedDescription,
        CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await resolve(host, cancellationToken);

        // Every address must be allowed, not just one: the socket may try any of them.
        if (addresses.Length == 0 || !addresses.All(isAllowed))
            throw new DestinationRefusedException(host, allowedDescription);

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
