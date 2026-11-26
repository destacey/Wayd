using System.Net;
using System.Net.Sockets;
using Wayd.Infrastructure.Networking;

namespace Wayd.Infrastructure.Tests.Sut.Networking;

/// <summary>
/// Holds the outbound guard for client-chosen URLs to the public internet: the addresses a server-side request
/// forgery would aim at are refused, both as a classification and when a connection is actually made.
/// </summary>
public sealed class PublicNetworkTests
{
    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("::ffff:8.8.8.8")]
    public void IsPublic_AcceptsAPubliclyRoutableAddress(string address)
    {
        // Arrange
        var ip = IPAddress.Parse(address);

        // Act
        var result = PublicNetwork.IsPublic(ip);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("ff02::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("64:ff9b::a00:1")]
    [InlineData("2002:a00:1::")]
    [InlineData("::a00:1")]
    [InlineData("fec0::1")]
    [InlineData("3fff::1")]
    public void IsPublic_RefusesAnAddressThatIsNotPubliclyRoutable(string address)
    {
        // Arrange
        var ip = IPAddress.Parse(address);

        // Act
        var result = PublicNetwork.IsPublic(ip);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("https://localhost:1/client.json")]
    [InlineData("https://127.0.0.1:1/client.json")]
    [InlineData("https://[::1]:1/client.json")]
    public async Task CreateHandler_RefusesToConnectToANonPublicAddress(string url)
    {
        // Arrange
        using var client = new HttpClient(PublicNetwork.CreateHandler(TimeSpan.FromSeconds(5)));

        // Act
        var send = () => client.GetAsync(url, TestContext.Current.CancellationToken);

        // Assert
        (await send.Should().ThrowAsync<HttpRequestException>())
            .Which.ToString().Should().Contain("does not resolve to a public address");
    }

    [Theory]
    [InlineData("10.0.0.1")]
    [InlineData("8.8.8.8", "10.0.0.1")]
    [InlineData("8.8.8.8", "::1")]
    public async Task CreateHandler_RefusesAHostName_WhenAnyAddressItResolvesToIsNotPublic(params string[] resolvedTo)
    {
        // Arrange — the socket may try any resolved address, so one private address among public ones is enough
        var addresses = resolvedTo.Select(IPAddress.Parse).ToArray();
        using var client = new HttpClient(PublicNetwork.CreateHandler(TimeSpan.FromSeconds(5), (_, _) => Task.FromResult(addresses)));

        // Act
        var send = () => client.GetAsync("https://client.acme.example/client.json", TestContext.Current.CancellationToken);

        // Assert
        (await send.Should().ThrowAsync<HttpRequestException>())
            .Which.ToString().Should().Contain("'client.acme.example' does not resolve to a public address");
    }

    [Theory]
    [InlineData("10.1.2.3")]
    [InlineData("192.168.1.1")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("fd12::1")]
    [InlineData("8.8.8.8")]
    [InlineData("100.64.0.1")]
    [InlineData("64:ff9b::a00:1")]
    [InlineData("2002:a00:1::")]
    [InlineData("::ffff:0:a00:1")]
    public void IsReachableOnPrivateNetwork_AcceptsAPrivateOrPublicAddress(string address)
    {
        // Arrange
        var ip = IPAddress.Parse(address);

        // Act
        var result = PublicNetwork.IsReachableOnPrivateNetwork(ip);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("169.254.169.254")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("fe80::1")]
    [InlineData("fd00:ec2::254")]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("224.0.0.1")]
    [InlineData("100.100.100.200")]
    [InlineData("64:ff9b::a9fe:a9fe")]
    [InlineData("2002:a9fe:a9fe::")]
    [InlineData("::a9fe:a9fe")]
    [InlineData("::ffff:0:a9fe:a9fe")]
    [InlineData("64:ff9b:1::a9fe:a9fe")]
    [InlineData("2001:0:4136:e378:8000:63bf:3fff:fdd2")]
    public void IsReachableOnPrivateNetwork_RefusesMetadataUnspecifiedAndMulticastAddresses(string address)
    {
        // Arrange
        var ip = IPAddress.Parse(address);

        // Act
        var result = PublicNetwork.IsReachableOnPrivateNetwork(ip);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CreatePrivateNetworkHandler_RefusesAHostNameThatResolvesToCloudMetadata()
    {
        // Arrange
        IPAddress[] addresses = [IPAddress.Parse("10.0.0.5"), IPAddress.Parse("169.254.169.254")];
        using var client = new HttpClient(PublicNetwork.CreatePrivateNetworkHandler(TimeSpan.FromSeconds(5), (_, _) => Task.FromResult(addresses)));

        // Act
        var send = () => client.GetAsync("https://idp.corp.example/.well-known/openid-configuration", TestContext.Current.CancellationToken);

        // Assert
        (await send.Should().ThrowAsync<HttpRequestException>())
            .Which.ToString().Should().Contain("'idp.corp.example' does not resolve to an allowed address");
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public async Task CreateHandler_ThroughAProxyAtAnIPLiteral_SendsARequestForAPublicHostToTheProxy(string proxyAddress)
    {
        // Arrange — the proxy is on loopback, which the destination check would refuse: its own address is exempt
        using var proxy = new FakeProxy(IPAddress.Parse(proxyAddress));
        IPAddress[] addresses = [IPAddress.Parse("8.8.8.8")];
        using var client = new HttpClient(PublicNetwork.CreateHandler(TimeSpan.FromSeconds(5), (_, _) => Task.FromResult(addresses), proxy.Address));
        var tunnel = proxy.RefuseNextTunnel();

        // Act
        var send = () => client.GetAsync("https://idp.acme.example/.well-known/openid-configuration", TestContext.Current.CancellationToken);

        // Assert — the fake proxy refuses the tunnel, so the request fails only after reaching it
        await send.Should().ThrowAsync<HttpRequestException>();
        (await tunnel).Should().Be("CONNECT idp.acme.example:443 HTTP/1.1");
    }

    [Theory]
    [InlineData("https://idp.acme.example/", "10.0.0.1")]
    [InlineData("https://idp.acme.example/", "169.254.169.254")]
    [InlineData("https://idp.acme.example/", "8.8.8.8", "127.0.0.1")]
    [InlineData("https://10.0.0.1/")]
    [InlineData("https://169.254.169.254/latest/meta-data/")]
    [InlineData("https://[::1]/")]
    public async Task CreateHandler_ThroughAProxy_RefusesANonPublicDestinationBeforeReachingTheProxy(string url, params string[] resolvedTo)
    {
        // Arrange
        using var proxy = new FakeProxy();
        var addresses = resolvedTo.Select(IPAddress.Parse).ToArray();
        using var client = new HttpClient(PublicNetwork.CreateHandler(TimeSpan.FromSeconds(5), (_, _) => Task.FromResult(addresses), proxy.Address));

        // Act
        var send = () => client.GetAsync(url, TestContext.Current.CancellationToken);

        // Assert
        (await send.Should().ThrowAsync<HttpRequestException>())
            .Which.ToString().Should().Contain("does not resolve to a public address");
        proxy.WasConnectedTo.Should().BeFalse();
    }

    [Fact]
    public async Task CreateHandler_ThroughAProxy_ReportsAHostThatDoesNotResolveAsAnHttpRequestException()
    {
        // Arrange
        using var proxy = new FakeProxy();
        using var client = new HttpClient(PublicNetwork.CreateHandler(
            TimeSpan.FromSeconds(5),
            (_, _) => Task.FromException<IPAddress[]>(new SocketException((int)SocketError.HostNotFound)),
            proxy.Address));

        // Act
        var send = () => client.GetAsync("https://missing.acme.example/", TestContext.Current.CancellationToken);

        // Assert
        (await send.Should().ThrowAsync<HttpRequestException>())
            .Which.HttpRequestError.Should().Be(HttpRequestError.NameResolutionError);
        proxy.WasConnectedTo.Should().BeFalse();
    }

    [Fact]
    public async Task CreatePrivateNetworkHandler_ThroughAProxy_SendsARequestForAPrivateHostToTheProxy()
    {
        // Arrange
        using var proxy = new FakeProxy();
        IPAddress[] addresses = [IPAddress.Parse("10.0.0.5")];
        using var client = new HttpClient(PublicNetwork.CreatePrivateNetworkHandler(TimeSpan.FromSeconds(5), (_, _) => Task.FromResult(addresses), proxy.Address));
        var tunnel = proxy.RefuseNextTunnel();

        // Act
        var send = () => client.GetAsync("https://idp.corp.example/.well-known/openid-configuration", TestContext.Current.CancellationToken);

        // Assert
        await send.Should().ThrowAsync<HttpRequestException>();
        (await tunnel).Should().Be("CONNECT idp.corp.example:443 HTTP/1.1");
    }

    [Fact]
    public async Task CreatePrivateNetworkHandler_ThroughAProxy_RefusesAHostNameThatResolvesToCloudMetadata()
    {
        // Arrange
        using var proxy = new FakeProxy();
        IPAddress[] addresses = [IPAddress.Parse("10.0.0.5"), IPAddress.Parse("169.254.169.254")];
        using var client = new HttpClient(PublicNetwork.CreatePrivateNetworkHandler(TimeSpan.FromSeconds(5), (_, _) => Task.FromResult(addresses), proxy.Address));

        // Act
        var send = () => client.GetAsync("https://idp.corp.example/.well-known/openid-configuration", TestContext.Current.CancellationToken);

        // Assert
        (await send.Should().ThrowAsync<HttpRequestException>())
            .Which.ToString().Should().Contain("'idp.corp.example' does not resolve to an allowed address");
        proxy.WasConnectedTo.Should().BeFalse();
    }

    /// <summary>A loopback listener standing in for an outbound proxy: it records the first request line and refuses the tunnel.</summary>
    private sealed class FakeProxy : IDisposable
    {
        private readonly TcpListener _listener;

        public FakeProxy(IPAddress? address = null)
        {
            _listener = new TcpListener(address ?? IPAddress.Loopback, 0);
            _listener.Start();
        }

        public Uri Address
        {
            get
            {
                var endpoint = (IPEndPoint)_listener.LocalEndpoint;
                return new UriBuilder("http", endpoint.Address.ToString(), endpoint.Port).Uri;
            }
        }

        /// <summary>Whether a connection is waiting to be accepted, which only a request sent on to the proxy makes.</summary>
        public bool WasConnectedTo => _listener.Pending();

        /// <summary>Accepts the next connection, answers its request with 502 and returns the request's first line.</summary>
        public async Task<string?> RefuseNextTunnel()
        {
            // Bounded, so a request that never reaches the proxy fails the test instead of hanging it.
            using var connection = await _listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            var stream = connection.GetStream();
            var line = await new StreamReader(stream).ReadLineAsync(TestContext.Current.CancellationToken);
            await stream.WriteAsync("HTTP/1.1 502 Bad Gateway\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray(), TestContext.Current.CancellationToken);
            return line;
        }

        public void Dispose() => _listener.Stop();
    }
}
