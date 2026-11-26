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
    /// A handler that reaches only public addresses, follows no redirects, sends no cookies and ignores any system
    /// proxy; with <paramref name="proxy"/> it sends every request through that proxy instead of connecting
    /// directly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Without a proxy the check runs when the connection is made, against the addresses the connection actually
    /// uses, so a host name that resolves to a public address when checked and a private one when connected (DNS
    /// rebinding) is still refused.
    /// </para>
    /// <para>
    /// Through a proxy the connection is to the proxy, so the destination is checked before the request is sent,
    /// and the socket may connect to nowhere but the proxy, whose own address is exempt. The proxy resolves the
    /// destination again, so a host that answers the API with a public address and the proxy with a private one
    /// gets through; refusing private destinations at the proxy closes that.
    /// </para>
    /// </remarks>
    /// <param name="connectTimeout">How long establishing a connection may take.</param>
    /// <param name="resolve">Resolves a host name to its addresses; DNS unless a test supplies its own.</param>
    /// <param name="proxy">The operator's outbound proxy (<see cref="OutboundProxyOptions.Address"/>), or null to connect directly.</param>
    public static HttpMessageHandler CreateHandler(
        TimeSpan connectTimeout,
        Func<string, CancellationToken, Task<IPAddress[]>>? resolve = null,
        Uri? proxy = null) =>
        CreateHandler(connectTimeout, IsPublic, "a public address", resolve, proxy);

    /// <summary>
    /// <see cref="CreateHandler(TimeSpan, Func{string, CancellationToken, Task{IPAddress[]}}?, Uri?)"/> for a
    /// destination the operator has allowed on the deployment's own network: private and loopback addresses are
    /// reached too, but never <see cref="IsReachableOnPrivateNetwork">cloud metadata, unspecified or multicast</see> ones.
    /// </summary>
    public static HttpMessageHandler CreatePrivateNetworkHandler(
        TimeSpan connectTimeout,
        Func<string, CancellationToken, Task<IPAddress[]>>? resolve = null,
        Uri? proxy = null) =>
        CreateHandler(connectTimeout, IsReachableOnPrivateNetwork, "an allowed address", resolve, proxy);

    private static HttpMessageHandler CreateHandler(
        TimeSpan connectTimeout,
        Func<IPAddress, bool> isAllowed,
        string allowedDescription,
        Func<string, CancellationToken, Task<IPAddress[]>>? resolve,
        Uri? proxy)
    {
        resolve ??= Dns.GetHostAddressesAsync;
        var sockets = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            ConnectTimeout = connectTimeout,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };

        if (proxy is null)
        {
            sockets.UseProxy = false;
            sockets.ConnectCallback = (context, cancellationToken) => Connect(context, resolve, isAllowed, allowedDescription, cancellationToken);
            return sockets;
        }

        sockets.UseProxy = true;
        sockets.Proxy = new FixedProxy(proxy);
        sockets.ConnectCallback = (context, cancellationToken) => ConnectToProxy(context, proxy, resolve, cancellationToken);
        return new DestinationCheck(sockets, resolve, isAllowed, allowedDescription);
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
        var addresses = await ResolveAllowed(host, resolve, isAllowed, allowedDescription, cancellationToken);
        return await ConnectSocket(addresses, context.DnsEndPoint.Port, cancellationToken);
    }

    /// <summary>
    /// Connects to the proxy, and refuses any other endpoint: the handler is meant to send everything through it,
    /// and a connection elsewhere would skip the destination check's premise.
    /// </summary>
    private static async ValueTask<Stream> ConnectToProxy(
        SocketsHttpConnectionContext context,
        Uri proxy,
        Func<string, CancellationToken, Task<IPAddress[]>> resolve,
        CancellationToken cancellationToken)
    {
        // An IPv6 literal may arrive with or without its brackets on either side.
        var host = context.DnsEndPoint.Host.Trim('[', ']');
        if (!string.Equals(host, proxy.IdnHost.Trim('[', ']'), StringComparison.OrdinalIgnoreCase) || context.DnsEndPoint.Port != proxy.Port)
            throw new DestinationRefusedException(host, "the outbound proxy");

        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await resolve(host, cancellationToken);

        return await ConnectSocket(addresses, context.DnsEndPoint.Port, cancellationToken);
    }

    /// <summary>The addresses <paramref name="host"/> resolves to, when every one of them is allowed.</summary>
    private static async Task<IPAddress[]> ResolveAllowed(
        string host,
        Func<string, CancellationToken, Task<IPAddress[]>> resolve,
        Func<IPAddress, bool> isAllowed,
        string allowedDescription,
        CancellationToken cancellationToken)
    {
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await resolve(host, cancellationToken);

        // Every address must be allowed, not just one: the socket may try any of them.
        if (addresses.Length == 0 || !addresses.All(isAllowed))
            throw new DestinationRefusedException(host, allowedDescription);

        return addresses;
    }

    private static async ValueTask<Stream> ConnectSocket(IPAddress[] addresses, int port, CancellationToken cancellationToken)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>Sends a request on only when its destination host resolves to allowed addresses.</summary>
    private sealed class DestinationCheck(
        HttpMessageHandler inner,
        Func<string, CancellationToken, Task<IPAddress[]>> resolve,
        Func<IPAddress, bool> isAllowed,
        string allowedDescription) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri ?? throw new InvalidOperationException("The request has no URI.");

            // DnsSafeHost strips an IPv6 literal's brackets, so it parses as the address it names.
            var host = uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6 ? uri.DnsSafeHost : uri.IdnHost;
            try
            {
                await ResolveAllowed(host, resolve, isAllowed, allowedDescription, cancellationToken);
            }
            catch (SocketException ex)
            {
                // Callers and the resilience handler expect a lookup failure as HttpRequestException, as
                // SocketsHttpHandler reports one made while connecting.
                throw new HttpRequestException(HttpRequestError.NameResolutionError, $"'{host}' could not be resolved.", ex);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }

    /// <summary>
    /// A proxy that every request goes through. <see cref="WebProxy"/> would send a loopback destination around
    /// the proxy, straight past the destination check's assumption that the socket only ever reaches the proxy.
    /// </summary>
    private sealed class FixedProxy(Uri address) : IWebProxy
    {
        public ICredentials? Credentials { get; set; }

        public Uri GetProxy(Uri destination) => address;

        public bool IsBypassed(Uri host) => false;
    }
}
